using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.Auth;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Domain.Common;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// The access audit (lens-set feedback ticket 03): what three kinds of caller can reach across
/// <em>every</em> controller action, API and Admin Portal alike.
///
/// <list type="bullet">
/// <item>(a) an account with no org assignment at all — both roles, since an Admin with no
/// assignment is the more dangerous half;</item>
/// <item>(b) an account assigned only above Retail Point, on the Field App API;</item>
/// <item>(c) a Retail Point <c>User</c> on the Admin Portal.</item>
/// </list>
///
/// The two tables below are the audit. Each row names one action, how to call it against real
/// seeded rows, and what each caller must get back. <see cref="EveryControllerAction_IsInTheAudit"/>
/// walks the application's own action descriptors, so an action added without a row here fails —
/// the audit cannot quietly fall behind the application.
///
/// Outcomes are stated as what the caller ends up with (denied, no rows, only their own rows,
/// nothing written), never as which filter or handler produced it. Each caller's whole table runs
/// in one test and reports every mismatch together, so one run shows the full extent of a
/// regression rather than its first symptom.
/// </summary>
public class AccessAuditTests(AccessAuditFixture fixture) : IClassFixture<AccessAuditFixture>
{
    private enum Expect
    {
        /// <summary>Not part of this caller's audit.</summary>
        NotApplicable,

        /// <summary>Reachable by design and holding nothing scoped: an anonymous action, or one
        /// that acts only on the caller's own account. Must not error, deny or show a scoped row.</summary>
        Open,

        /// <summary>A global library (reference data), readable by any authenticated caller.</summary>
        GlobalLibrary,

        /// <summary>Portal: lands on /Account/AccessDenied.</summary>
        Denied,

        /// <summary>Portal: either Denied or a re-rendered form; in both cases nothing is written.</summary>
        Refused,

        /// <summary>Succeeds, and shows no hierarchy-scoped row at all (API: an empty list).</summary>
        NoRows,

        /// <summary>Portal: succeeds, shows the caller's own row and nothing outside their subtree.</summary>
        OwnRowsOnly,

        NotFound,
        NoContent,
        BadRequest,

        /// <summary>API create: refused with the CurrentLocationCheck reason; nothing is written.</summary>
        RefusedForLocation,
    }

    private sealed record Caller(AccessAuditFixture.Account Account, string Description, string? LocationRefusal = null);

    /// <param name="Action">"{Portal|Api}.{Controller}.{Method} {VERB}" — the coverage key.</param>
    /// <param name="NoAssignment">What caller (a) gets.</param>
    /// <param name="OutletUser">What caller (c) gets.</param>
    /// <param name="OwnMarker">Text caller (c) must see when the outcome is OwnRowsOnly.</param>
    /// <param name="Written">True if the write went through — must be false for any refusal.</param>
    private sealed record PortalRow(
        string Action,
        Func<AccessAuditFixture, string> Url,
        Expect NoAssignment,
        Expect OutletUser,
        Func<AccessAuditFixture, Caller, (string Key, string Value)[]>? Fields = null,
        string? OwnMarker = null,
        Func<AccessAuditFixture, Caller, DotGlassesDbContext, Task<bool>>? Written = null);

    /// <param name="NoAssignment">What caller (a) gets.</param>
    /// <param name="AboveRetailPoint">What caller (b) gets.</param>
    private sealed record ApiRow(
        string Action,
        Func<AccessAuditFixture, string> Url,
        Expect NoAssignment,
        Expect AboveRetailPoint,
        Func<AccessAuditFixture, Guid, object>? Body = null,
        Func<Guid, DotGlassesDbContext, Task<bool>>? Written = null);

    private static readonly Guid KenyaRetailPointId = OrganisationSeedConfiguration.KenyaRetailPointId;

    // ---------------------------------------------------------------------------------------------
    // The Admin Portal (cookie). Every write targets a real row, so a refusal is the access rule at
    // work rather than a missing target.
    // ---------------------------------------------------------------------------------------------

    private static readonly PortalRow[] PortalRows =
    [
        // Dashboard
        new("Portal.Home.Index GET", _ => "/", Expect.NoRows, Expect.OwnRowsOnly),
        new("Portal.Home.Error GET", _ => "/Home/Error", Expect.Open, Expect.Open),

        // Account — anonymous, or the caller's own account only.
        new("Portal.Account.Login GET", _ => "/Account/Login", Expect.Open, Expect.Open),
        new("Portal.Account.Login POST", _ => "/Account/Login", Expect.Open, Expect.Open,
            (_, _) => [("UserName", "nobody@test.local"), ("Password", "not-the-password")]),
        new("Portal.Account.AccessDenied GET", _ => "/Account/AccessDenied", Expect.Open, Expect.Open),
        new("Portal.Account.SetPassword GET", _ => "/Account/SetPassword?userId=x&token=y", Expect.Open, Expect.Open),
        new("Portal.Account.SetPassword POST", _ => "/Account/SetPassword", Expect.Open, Expect.Open,
            (_, _) => [("UserId", Guid.NewGuid().ToString()), ("Token", "not-a-token"), ("Password", "An0ther!Passw0rd"), ("ConfirmPassword", "An0ther!Passw0rd")]),
        new("Portal.Account.Settings GET", _ => "/Account/Settings", Expect.Open, Expect.Open),
        new("Portal.Account.ChangePassword POST", _ => "/Account/ChangePassword", Expect.Open, Expect.Open,
            (_, _) => [("CurrentPassword", "not-the-password"), ("NewPassword", "An0ther!Passw0rd"), ("ConfirmPassword", "An0ther!Passw0rd")]),
        new("Portal.Account.Logout POST", _ => "/Account/Logout", Expect.Open, Expect.Open, (_, _) => []),

        // Organisations — [Authorize] only: scoped reads, resource-checked writes.
        new("Portal.Organisations.Index GET", _ => "/Organisations", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnOutletNameFragment),
        new("Portal.Organisations.Index GET", f => $"/Organisations?selectedId={f.SiblingOutletId}", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnOutletNameFragment),
        new("Portal.Organisations.Export GET", _ => "/Organisations/Export", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnOutletNameFragment),
        new("Portal.Organisations.CreateChild POST", _ => "/Organisations/CreateChild", Expect.Denied, Expect.Denied,
            (_, c) => [("ParentId", OrganisationSeedConfiguration.KenyaRetailerId.ToString()), ("Name", $"Made by {c.Account.UserId:N}"), ("Level", "RetailPoint")],
            Written: (_, c, db) => db.OrganisationNodes.IgnoreQueryFilters().AnyAsync(o => o.Name == $"Made by {c.Account.UserId:N}")),
        new("Portal.Organisations.CreateChild POST", _ => "/Organisations/CreateChild", Expect.Denied, Expect.Denied,
            (_, c) => [("ParentId", KenyaRetailPointId.ToString()), ("Name", $"Made under own outlet by {c.Account.UserId:N}"), ("Level", "RetailPoint")],
            Written: (_, c, db) => db.OrganisationNodes.IgnoreQueryFilters().AnyAsync(o => o.Name == $"Made under own outlet by {c.Account.UserId:N}")),
        new("Portal.Organisations.SetTrainingOrgFlag POST", _ => "/Organisations/SetTrainingOrgFlag", Expect.Denied, Expect.Denied,
            (_, _) => [("id", KenyaRetailPointId.ToString()), ("value", "true")],
            Written: (_, _, db) => db.OrganisationNodes.IgnoreQueryFilters().AnyAsync(o => o.Id == KenyaRetailPointId && o.IsTrainingOrg)),
        // The escalation that matters most: a caller assigning *themselves* somewhere.
        new("Portal.Organisations.AssignUser POST", _ => "/Organisations/AssignUser", Expect.Denied, Expect.Denied,
            (f, c) => [("orgNodeId", f.SiblingOutletId.ToString()), ("userId", c.Account.UserId.ToString())],
            Written: (f, c, db) => db.UserOrgAssignments.AnyAsync(a => a.UserId == c.Account.UserId && a.OrgNodeId == f.SiblingOutletId)),
        new("Portal.Organisations.AssignUser POST", _ => "/Organisations/AssignUser", Expect.Denied, Expect.Denied,
            (f, _) => [("orgNodeId", KenyaRetailPointId.ToString()), ("userId", f.OutOfScopeTargetUserId.ToString())],
            Written: (f, _, db) => db.UserOrgAssignments.AnyAsync(a => a.UserId == f.OutOfScopeTargetUserId && a.OrgNodeId == KenyaRetailPointId)),
        new("Portal.Organisations.UnassignUser POST", _ => "/Organisations/UnassignUser", Expect.Denied, Expect.Denied,
            (f, _) => [("orgNodeId", KenyaRetailPointId.ToString()), ("userId", f.InScopeTargetUserId.ToString())],
            Written: async (f, _, db) => !await db.UserOrgAssignments.AnyAsync(a => a.UserId == f.InScopeTargetUserId && a.OrgNodeId == KenyaRetailPointId)),
        new("Portal.Organisations.Rename POST", _ => "/Organisations/Rename", Expect.Denied, Expect.Denied,
            (_, _) => [("Id", KenyaRetailPointId.ToString()), ("Name", "Renamed by the audit")],
            Written: (_, _, db) => db.OrganisationNodes.IgnoreQueryFilters().AnyAsync(o => o.Name == "Renamed by the audit")),
        new("Portal.Organisations.SetActive POST", _ => "/Organisations/SetActive", Expect.Denied, Expect.Denied,
            (_, _) => [("id", KenyaRetailPointId.ToString()), ("value", "false")],
            Written: (_, _, db) => db.OrganisationNodes.IgnoreQueryFilters().AnyAsync(o => o.Id == KenyaRetailPointId && o.IsDeleted)),
        // Reactivation finds its target through its own unfiltered, hand-scoped query.
        new("Portal.Organisations.SetActive POST", _ => "/Organisations/SetActive", Expect.Denied, Expect.Denied,
            (f, _) => [("id", f.DeactivatedOutletId.ToString()), ("value", "true")],
            Written: (f, _, db) => db.OrganisationNodes.IgnoreQueryFilters().AnyAsync(o => o.Id == f.DeactivatedOutletId && !o.IsDeleted)),

        // Event History — [Authorize] only, scoped reads. Every tab, since each is its own query.
        new("Portal.EventHistory.Index GET", _ => "/EventHistory", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnCustomerName),
        new("Portal.EventHistory.Index GET", _ => "/EventHistory?tab=tests", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnOutletNameFragment),
        new("Portal.EventHistory.Index GET", _ => "/EventHistory?tab=leads", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnCustomerName),
        new("Portal.EventHistory.Index GET", _ => "/EventHistory?tab=leads&search=Foreign", Expect.NoRows, Expect.OwnRowsOnly),
        new("Portal.EventHistory.Index GET", _ => "/EventHistory?tab=referrals", Expect.NoRows, Expect.OwnRowsOnly),
        new("Portal.EventHistory.Export GET", _ => "/EventHistory/Export", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnCustomerName),
        new("Portal.EventHistory.Export GET", _ => "/EventHistory/Export?tab=tests", Expect.NoRows, Expect.OwnRowsOnly),
        new("Portal.EventHistory.Export GET", _ => "/EventHistory/Export?tab=leads", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnCustomerName),
        new("Portal.EventHistory.Export GET", _ => "/EventHistory/Export?tab=referrals", Expect.NoRows, Expect.OwnRowsOnly),

        // Lead conversion — [Authorize] only, and deliberately open to any role inside its scope
        // (see LeadConversionController's doc comment): a lead outside the caller's subtree does
        // not exist as far as they can tell. (c)'s own-lead POST is not exercised — it is the one
        // write the portal lets a User make, and making it would consume the fixture's lead.
        new("Portal.LeadConversion.Convert GET", f => $"/Leads/Convert/{f.Own.LeadId}", Expect.NotFound, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OwnCustomerName),
        new("Portal.LeadConversion.Convert GET", f => $"/Leads/Convert/{f.Foreign.LeadId}", Expect.NotFound, Expect.NotFound),
        new("Portal.LeadConversion.Coatings GET", f => $"/Leads/Convert/{f.Own.LeadId}/coatings", Expect.NotFound, Expect.Open),
        new("Portal.LeadConversion.Coatings GET", f => $"/Leads/Convert/{f.Foreign.LeadId}/coatings", Expect.NotFound, Expect.NotFound),
        new("Portal.LeadConversion.Convert POST", f => $"/Leads/Convert/{f.Own.LeadId}", Expect.NotFound, Expect.NotApplicable, (_, _) => [],
            Written: (f, _, db) => db.Sales.IgnoreQueryFilters().AnyAsync(s => s.SourceLeadId == f.Own.LeadId)),
        new("Portal.LeadConversion.Convert POST", f => $"/Leads/Convert/{f.Foreign.LeadId}", Expect.NotFound, Expect.NotFound, (_, _) => [],
            Written: (f, _, db) => db.Sales.IgnoreQueryFilters().AnyAsync(s => s.SourceLeadId == f.Foreign.LeadId)),

        // User Directory — [Authorize] only: a manually scoped read, resource-checked writes.
        new("Portal.UserDirectory.Index GET", _ => "/UserDirectory", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OutletTargetUser),
        new("Portal.UserDirectory.Index GET", _ => "/UserDirectory?search=target", Expect.NoRows, Expect.OwnRowsOnly, OwnMarker: AccessAuditFixture.OutletTargetUser),
        new("Portal.UserDirectory.Invite POST", _ => "/UserDirectory/Invite", Expect.Refused, Expect.Denied,
            (_, c) => [("Email", InvitedEmail(c)), ("FullName", "Invited By The Audit"), ("Role", RoleNames.Admin), ("OrgNodeIds", KenyaRetailPointId.ToString())],
            Written: (_, c, db) => db.Users.AnyAsync(u => u.Email == InvitedEmail(c))),
        new("Portal.UserDirectory.ResetPassword POST", _ => "/UserDirectory/ResetPassword", Expect.Denied, Expect.Denied,
            (f, _) => [("id", f.InScopeTargetUserId.ToString())]),
        new("Portal.UserDirectory.Suspend POST", _ => "/UserDirectory/Suspend", Expect.Denied, Expect.Denied,
            (f, _) => [("id", f.InScopeTargetUserId.ToString())],
            Written: (f, _, db) => db.Users.AnyAsync(u => u.Id == f.InScopeTargetUserId && u.LockoutEnd != null)),
        new("Portal.UserDirectory.Unsuspend POST", _ => "/UserDirectory/Unsuspend", Expect.Denied, Expect.Denied,
            (f, _) => [("id", f.InScopeTargetUserId.ToString())]),
        new("Portal.UserDirectory.ChangeRole POST", _ => "/UserDirectory/ChangeRole", Expect.Denied, Expect.Denied,
            (f, _) => [("id", f.InScopeTargetUserId.ToString()), ("role", RoleNames.Admin)],
            Written: (f, _, db) => db.UserRoles.AnyAsync(ur => ur.UserId == f.InScopeTargetUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Admin))),
        // ...and a caller promoting themselves.
        new("Portal.UserDirectory.ChangeRole POST", _ => "/UserDirectory/ChangeRole", Expect.Denied, Expect.Denied,
            (_, c) => [("id", c.Account.UserId.ToString()), ("role", RoleNames.Admin)]),

        // Policy-gated screens: level-gated, so closed to (a), who has no level, and to (c).
        new("Portal.CustomOrders.Index GET", _ => "/CustomOrders", Expect.Denied, Expect.Denied),
        new("Portal.CustomOrders.Export GET", _ => "/CustomOrders/Export", Expect.Denied, Expect.Denied),
        new("Portal.CustomOrders.AdvanceStatus POST", _ => "/CustomOrders/AdvanceStatus", Expect.Denied, Expect.Denied,
            (f, _) => [("saleId", f.Own.SaleId.ToString())],
            Written: (f, _, db) => db.Sales.IgnoreQueryFilters().AnyAsync(s => s.Id == f.Own.SaleId && s.FulfilmentStatus != Domain.Enums.FulfilmentStatus.Submitted)),

        new("Portal.Catalogues.Index GET", _ => "/Catalogues", Expect.Denied, Expect.Denied),
        new("Portal.Catalogues.Details GET", _ => $"/Catalogues/Details/{ExampleLensSets.SixLensSetId}", Expect.Denied, Expect.Denied),
        new("Portal.Catalogues.LensPowers GET", _ => "/Catalogues/LensPowers", Expect.Denied, Expect.Denied),
        new("Portal.Catalogues.CreateCatalogue POST", _ => "/Catalogues/CreateCatalogue", Expect.Denied, Expect.Denied,
            (_, c) => [("Name", $"Lens set by {c.Account.UserId:N}"), ("OwningOrgNodeId", KenyaRetailPointId.ToString())],
            Written: (_, c, db) => db.PresetCatalogues.IgnoreQueryFilters().AnyAsync(p => p.Name == $"Lens set by {c.Account.UserId:N}")),
        new("Portal.Catalogues.UpdateCatalogue POST", _ => "/Catalogues/UpdateCatalogue", Expect.Denied, Expect.Denied,
            (_, _) => [("Id", ExampleLensSets.SixLensSetId.ToString()), ("Name", "Renamed by the audit")],
            Written: (_, _, db) => db.PresetCatalogues.IgnoreQueryFilters().AnyAsync(p => p.Name == "Renamed by the audit")),
        new("Portal.Catalogues.SaveLens POST", _ => "/Catalogues/SaveLens", Expect.Denied, Expect.Denied,
            (_, _) => [("PresetCatalogueId", ExampleLensSets.SixLensSetId.ToString()), ("Label", "Audit lens")]),
        new("Portal.Catalogues.RemoveLensOption POST", _ => "/Catalogues/RemoveLensOption", Expect.Denied, Expect.Denied,
            (_, _) => [("lensOptionId", ExampleLensSets.SixLensPlus250Id.ToString())],
            Written: async (_, _, db) => !await db.LensOptions.IgnoreQueryFilters().AnyAsync(l => l.Id == ExampleLensSets.SixLensPlus250Id)),
        new("Portal.Catalogues.AssignCatalogue POST", _ => "/Catalogues/AssignCatalogue", Expect.Denied, Expect.Denied,
            (_, _) => [("OrgNodeId", KenyaRetailPointId.ToString()), ("CatalogueId", ExampleLensSets.SixLensSetId.ToString())]),
        new("Portal.Catalogues.RetireCatalogue POST", _ => "/Catalogues/RetireCatalogue", Expect.Denied, Expect.Denied,
            (_, _) => [("catalogueId", ExampleLensSets.SixLensSetId.ToString())],
            Written: (_, _, db) => db.PresetCatalogues.IgnoreQueryFilters().AnyAsync(p => p.Id == ExampleLensSets.SixLensSetId && p.IsDeleted)),
        new("Portal.Catalogues.ReactivateCatalogue POST", _ => "/Catalogues/ReactivateCatalogue", Expect.Denied, Expect.Denied,
            (_, _) => [("catalogueId", ExampleLensSets.SixLensSetId.ToString())]),
        new("Portal.Catalogues.UnassignCatalogue POST", _ => "/Catalogues/UnassignCatalogue", Expect.Denied, Expect.Denied,
            (_, _) => [("catalogueId", ExampleLensSets.SixLensSetId.ToString()), ("orgNodeId", OrganisationSeedConfiguration.DgiId.ToString())]),

        new("Portal.ReferenceData.Index GET", _ => "/ReferenceData", Expect.Denied, Expect.Denied),
        new("Portal.ReferenceData.Create POST", _ => "/ReferenceData/Create", Expect.Denied, Expect.Denied,
            (_, _) => [("Category", "Occupation"), ("Label", "Added by the audit")],
            Written: (_, _, db) => db.ReferenceDataItems.AnyAsync(r => r.Label == "Added by the audit")),
        new("Portal.ReferenceData.Update POST", _ => "/ReferenceData/Update", Expect.Denied, Expect.Denied,
            (_, _) => [("Id", Guid.NewGuid().ToString()), ("Label", "Renamed by the audit")]),
        new("Portal.ReferenceData.MoveUp POST", _ => "/ReferenceData/MoveUp", Expect.Denied, Expect.Denied, (_, _) => [("id", Guid.NewGuid().ToString())]),
        new("Portal.ReferenceData.MoveDown POST", _ => "/ReferenceData/MoveDown", Expect.Denied, Expect.Denied, (_, _) => [("id", Guid.NewGuid().ToString())]),
        new("Portal.ReferenceData.Deactivate POST", _ => "/ReferenceData/Deactivate", Expect.Denied, Expect.Denied, (_, _) => [("id", Guid.NewGuid().ToString())]),
        new("Portal.ReferenceData.Reactivate POST", _ => "/ReferenceData/Reactivate", Expect.Denied, Expect.Denied, (_, _) => [("id", Guid.NewGuid().ToString())]),
        new("Portal.ReferenceData.AddCoatingExclusion POST", _ => "/ReferenceData/AddCoatingExclusion", Expect.Denied, Expect.Denied,
            (_, _) => [("coatingRefIdA", Guid.NewGuid().ToString()), ("coatingRefIdB", Guid.NewGuid().ToString())]),
        new("Portal.ReferenceData.RemoveCoatingExclusion POST", _ => "/ReferenceData/RemoveCoatingExclusion", Expect.Denied, Expect.Denied, (_, _) => [("id", Guid.NewGuid().ToString())]),
    ];

    // ---------------------------------------------------------------------------------------------
    // The Field App API (JWT). The rows under test are recorded at the Kenya retail point, which
    // sits *inside* caller (b)'s Admin Portal scope — so (b) seeing none of them here is the Field
    // App's scope being the current location alone, not the rows being out of reach anyway.
    // ---------------------------------------------------------------------------------------------

    private static readonly ApiRow[] ApiRows =
    [
        new("Api.Auth.Login POST", _ => "api/v1/auth/login", Expect.Open, Expect.Open,
            (_, _) => new LoginRequest { UserName = "nobody@test.local", Password = "not-the-password" }),
        new("Api.Auth.MyOrgs GET", _ => "api/v1/auth/my-orgs", Expect.NoRows, Expect.NoRows),
        // A retail point the caller isn't directly assigned to, then the org (b) *is* assigned to.
        new("Api.Auth.SwitchOrg POST", _ => "api/v1/auth/switch-org", Expect.BadRequest, Expect.BadRequest,
            (_, _) => new SwitchOrgRequest { OrgNodeId = KenyaRetailPointId }),
        new("Api.Auth.SwitchOrg POST", _ => "api/v1/auth/switch-org", Expect.BadRequest, Expect.BadRequest,
            (_, _) => new SwitchOrgRequest { OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId }),
        new("Api.Auth.ChangePassword POST", _ => "api/v1/auth/change-password", Expect.Open, Expect.Open,
            (_, _) => new ChangePasswordRequest { CurrentPassword = "not-the-password", NewPassword = "An0ther!Passw0rd" }),
        new("Api.ClientLogs.Post POST", _ => "api/v1/client-logs", Expect.Open, Expect.Open,
            (_, _) => new { correlationId = Guid.NewGuid(), entries = Array.Empty<object>() }),

        new("Api.Tests.List GET", _ => "api/v1/tests", Expect.NoRows, Expect.NoRows),
        new("Api.Tests.GetById GET", f => $"api/v1/tests/{f.Own.TestId}", Expect.NotFound, Expect.NotFound),
        new("Api.Tests.Create POST", _ => "api/v1/tests", Expect.RefusedForLocation, Expect.RefusedForLocation,
            (_, id) => new { id },
            (id, db) => db.Tests.IgnoreQueryFilters().AnyAsync(t => t.Id == id)),

        new("Api.Leads.List GET", _ => "api/v1/leads", Expect.NoRows, Expect.NoRows),
        new("Api.Leads.ListOpen GET", _ => "api/v1/leads/open", Expect.NoRows, Expect.NoRows),
        new("Api.Leads.GetById GET", f => $"api/v1/leads/{f.Own.LeadId}", Expect.NotFound, Expect.NotFound),
        new("Api.Leads.Match GET", _ => $"api/v1/leads/match?fullName={Uri.EscapeDataString(AccessAuditFixture.OwnCustomerName)}&phoneNumber={AccessAuditFixture.CustomerPhone}",
            Expect.NoContent, Expect.NoContent),
        new("Api.Leads.Create POST", _ => "api/v1/leads", Expect.RefusedForLocation, Expect.RefusedForLocation,
            (f, id) => new { id, sourceTestId = f.Own.TestId, fullName = "Audit Refused Lead" },
            (id, db) => db.Leads.IgnoreQueryFilters().AnyAsync(l => l.Id == id)),

        new("Api.Sales.List GET", _ => "api/v1/sales", Expect.NoRows, Expect.NoRows),
        new("Api.Sales.GetById GET", f => $"api/v1/sales/{f.Own.SaleId}", Expect.NotFound, Expect.NotFound),
        new("Api.Sales.Create POST", _ => "api/v1/sales", Expect.RefusedForLocation, Expect.RefusedForLocation,
            (f, id) => new { id, sourceLeadId = f.Own.LeadId, fullName = "Audit Refused Sale" },
            (id, db) => db.Sales.IgnoreQueryFilters().AnyAsync(s => s.Id == id)),

        // "The lens sets reaching their location" — none, with no location.
        new("Api.PresetCatalogues.List GET", _ => "api/v1/preset-catalogues", Expect.NoRows, Expect.NoRows),

        // Global libraries: not hierarchy-scoped, readable by any authenticated caller.
        new("Api.ReferenceData.List GET", _ => "api/v1/reference-data", Expect.GlobalLibrary, Expect.GlobalLibrary),
        new("Api.ReferenceData.CoatingRules GET", _ => "api/v1/reference-data/coating-rules", Expect.GlobalLibrary, Expect.GlobalLibrary),
    ];

    // --- Coverage ---------------------------------------------------------------------------------

    [Fact]
    public void EveryControllerAction_IsInTheAudit()
    {
        var actions = fixture.Factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(ActionKey)
            .ToHashSet();

        var audited = PortalRows.Select(r => r.Action).Concat(ApiRows.Select(r => r.Action)).ToHashSet();

        // Guards the guard: an empty descriptor list would make the next two assertions vacuous.
        Assert.True(actions.Count > 40, $"Only {actions.Count} controller actions were discovered.");

        var unaudited = actions.Except(audited).Order().ToList();
        Assert.True(unaudited.Count == 0,
            "These controller actions have no row in the access audit — add one to PortalRows/ApiRows saying what a " +
            "user with no assignment, a user above Retail Point and a Retail Point User each get:\n  " + string.Join("\n  ", unaudited));

        var stale = audited.Except(actions).Order().ToList();
        Assert.True(stale.Count == 0, "These audit rows name no controller action (renamed or removed?):\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void EveryAuditedAction_SaysWhatAUserWithNoAssignmentGets()
    {
        // (a) is the caller the ticket asks to be covered on every action without exception.
        Assert.DoesNotContain(PortalRows, r => r.NoAssignment == Expect.NotApplicable);
        Assert.DoesNotContain(ApiRows, r => r.NoAssignment == Expect.NotApplicable);
    }

    // --- The audit's own footing ------------------------------------------------------------------

    [Fact]
    public async Task TheRowsTheAuditExpectsNobodyToSee_AreThereForACallerWhoShouldSeeThem()
    {
        // Without this, "no rows" and "404" above would pass just as well against an empty database.
        var technician = fixture.Factory.CreateTechnicianClient(OrganisationSeedConfiguration.KenyaRetailPointPath);

        Assert.Contains(fixture.Own.LeadId, (await technician.GetFromJsonAsync<List<LeadDto>>("api/v1/leads"))!.Select(l => l.Id));
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync($"api/v1/tests/{fixture.Own.TestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync($"api/v1/sales/{fixture.Own.SaleId}")).StatusCode);
        Assert.NotEmpty((await technician.GetFromJsonAsync<List<PresetCatalogueDto>>("api/v1/preset-catalogues"))!);

        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        var leads = await dgiAdmin.GetStringAsync("/EventHistory?tab=leads");
        Assert.Contains(AccessAuditFixture.OwnCustomerName, leads);
        Assert.Contains(AccessAuditFixture.ForeignCustomerName, leads);
        Assert.Contains(AccessAuditFixture.DeactivatedOutletName, await dgiAdmin.GetStringAsync("/Organisations"));
    }

    // --- (a) No assignment at all ------------------------------------------------------------------

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.User)]
    public async Task AUserWithNoAssignment_OnTheAdminPortal_SeesNoScopedRow_PassesNoPolicy_AndWritesNothing(string role)
    {
        var caller = new Caller(await fixture.NewAccountWithNoAssignmentAsync(role), $"{role} with no assignment");

        var failures = await RunPortalAsync(caller, r => r.NoAssignment, forbiddenText: EverythingScoped);

        AssertNoFailures(failures);
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.User)]
    public async Task AUserWithNoAssignment_OnTheFieldAppApi_SeesNoScopedRow_AndCannotRecord(string role)
    {
        var account = await fixture.NewAccountWithNoAssignmentAsync(role);
        var retailPoint = await OrgAsync(KenyaRetailPointId);

        var failures = new List<string>();

        // The token the server would issue them: no location.
        failures.AddRange(await RunApiAsync(
            fixture.ApiClient(account, currentLocationId: null),
            new Caller(account, $"{role} with no assignment, token with no location", CurrentLocationCheck.NoLocation.RefusalMessage),
            r => r.NoAssignment));

        // A stale or tampered token still naming a retail point they are not assigned to.
        failures.AddRange(await RunApiAsync(
            fixture.ApiClient(account, KenyaRetailPointId),
            new Caller(account, $"{role} with no assignment, token naming a retail point", Refusal(CurrentLocationStatus.NoLongerAssigned, retailPoint)),
            r => r.NoAssignment));

        AssertNoFailures(failures);
    }

    [Fact]
    public async Task AUserWithNoAssignment_OpeningOrganisations_IsToldThereIsNothingToShow()
    {
        // An empty scope used to fail this screen outright — it selects the first tree, and there
        // was none. The scope filter had already answered "nothing"; the screen now says so.
        var account = await fixture.NewAccountWithNoAssignmentAsync(RoleNames.Admin);
        var client = await fixture.SignInAsync(account.UserName);

        var response = await client.GetAsync("/Organisations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("assigned to an organisation", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AUserWithNoAssignment_SigningInToTheFieldApp_IsIssuedATokenWithNoLocation()
    {
        var account = await fixture.NewAccountWithNoAssignmentAsync(RoleNames.User);

        var login = await SignInToFieldAppAsync(account);

        Assert.Null(login.CurrentLocationId);
        Assert.Null(login.CurrentLocationName);
    }

    // --- (b) Assigned only above Retail Point -----------------------------------------------------

    [Fact]
    public async Task AUserAssignedOnlyAboveRetailPoint_OnTheFieldAppApi_SeesNoScopedRow_AndEveryCreateIsRefusedForItsLocation()
    {
        var account = fixture.ResellerUser;
        var reseller = await OrgAsync(OrganisationSeedConfiguration.KenyaRetailerId);
        var retailPointBeneath = await OrgAsync(KenyaRetailPointId);

        var failures = new List<string>();

        // The token the server actually issues them: they have no eligible location, so none.
        var login = await SignInToFieldAppAsync(account);
        Assert.Null(login.CurrentLocationId);
        var signedIn = fixture.Factory.CreateClient();
        signedIn.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        failures.AddRange(await RunApiAsync(
            signedIn,
            new Caller(account, "reseller User, token as issued at sign-in", CurrentLocationCheck.NoLocation.RefusalMessage),
            r => r.AboveRetailPoint));

        // Tampered: naming the org they *are* assigned to, which is not a retail point.
        failures.AddRange(await RunApiAsync(
            fixture.ApiClient(account, OrganisationSeedConfiguration.KenyaRetailerId),
            new Caller(account, "reseller User, token naming the reseller", Refusal(CurrentLocationStatus.NotRetailPoint, reseller)),
            r => r.AboveRetailPoint));

        // Tampered: naming a retail point beneath their assignment — in their Admin Portal scope,
        // but a broad scope never widens where someone can record.
        failures.AddRange(await RunApiAsync(
            fixture.ApiClient(account, KenyaRetailPointId),
            new Caller(account, "reseller User, token naming a retail point beneath them", Refusal(CurrentLocationStatus.NoLongerAssigned, retailPointBeneath)),
            r => r.AboveRetailPoint));

        AssertNoFailures(failures);
    }

    [Fact]
    public async Task AUserAssignedOnlyAboveRetailPoint_StillUsesTheAdminPortalAtTheirLevel()
    {
        // The other half of (b): losing the ability to record must not cost them the portal.
        var client = await fixture.SignInAsync(fixture.ResellerUser.UserName);

        var leads = await client.GetStringAsync("/EventHistory?tab=leads");
        Assert.Contains(AccessAuditFixture.OwnCustomerName, leads);
        Assert.Contains(AccessAuditFixture.ForeignCustomerName, leads); // the sibling outlet is beneath the reseller too

        var organisations = await client.GetStringAsync("/Organisations");
        Assert.Contains(AccessAuditFixture.SiblingOutletName, organisations);
        Assert.DoesNotContain("Kampala Outlet", organisations);

        // Below Country, and not an Admin: no level-gated screen opens.
        foreach (var gated in new[] { "/CustomOrders", "/Catalogues", "/ReferenceData" })
        {
            Assert.Equal("/Account/AccessDenied", RedirectPath(await client.GetAsync(gated)));
        }
    }

    // --- (c) A Retail Point User on the Admin Portal ----------------------------------------------

    [Fact]
    public async Task ARetailPointUser_OnTheAdminPortal_SeesOnlyTheirOwnSubtree_AndIsRefusedEveryWriteAndEveryGatedScreen()
    {
        var caller = new Caller(fixture.OutletUser, "Retail Point User");

        var failures = await RunPortalAsync(caller, r => r.OutletUser, forbiddenText: OutsideTheOutlet);

        AssertNoFailures(failures);
    }

    // --- The two sign-ins don't cross over --------------------------------------------------------

    [Fact]
    public async Task AFieldAppToken_OpensNoAdminPortalScreen_AndAPortalSession_OpensNoApiEndpoint()
    {
        // The portal's scope is every assignment; the Field App's is one location. If either
        // sign-in were honoured on the other surface, a caller would get the wrong one of the two.
        var token = fixture.ApiClient(fixture.OutletUser, KenyaRetailPointId);
        foreach (var row in PortalRows.Where(r => r.Fields is null && r.OutletUser is Expect.OwnRowsOnly or Expect.Denied))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, row.Url(fixture));
            request.Headers.Authorization = token.DefaultRequestHeaders.Authorization;
            var response = await fixture.Factory.CreateClient(new() { AllowAutoRedirect = false }).SendAsync(request);
            Assert.True(RedirectPath(response) == "/Account/Login", $"{row.Action} answered a Field App token with {(int)response.StatusCode}.");
        }

        var session = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        foreach (var row in ApiRows.Where(r => r.Body is null))
        {
            var response = await session.GetAsync(row.Url(fixture));
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{row.Action} answered a portal session with {(int)response.StatusCode}.");
        }
    }

    // --- Running the tables -----------------------------------------------------------------------

    /// <summary>Text no caller with an empty scope should ever be shown: every seeded customer,
    /// every org below DGI by name, and other people's accounts.</summary>
    private static readonly string[] EverythingScoped =
    [
        AccessAuditFixture.OwnCustomerName, AccessAuditFixture.ForeignCustomerName,
        AccessAuditFixture.SiblingOutletName, AccessAuditFixture.DeactivatedOutletName, AccessAuditFixture.OwnOutletNameFragment,
        "Kangemi Vision Centre", "Kampala Outlet", "Uganda", "Kenya", "DOT Glasses International",
        AccessAuditFixture.OutletTargetUser, "uganda-target@test.local", "sibling-target@test.local", AccessControlFixture.DgiAdmin,
    ];

    /// <summary>Text the Kenya retail point's User should never be shown: what is beside or above
    /// their outlet. Their own outlet, its ancestors' names (which reports resolve on purpose) and
    /// the accounts assigned to it are theirs to see.</summary>
    private static readonly string[] OutsideTheOutlet =
    [
        AccessAuditFixture.ForeignCustomerName,
        AccessAuditFixture.SiblingOutletName, AccessAuditFixture.DeactivatedOutletName,
        "Kampala Outlet", "Uganda",
        "uganda-target@test.local", "sibling-target@test.local", AccessControlFixture.DgiAdmin, AccessControlFixture.CountryAdmin,
    ];

    private async Task<List<string>> RunPortalAsync(Caller caller, Func<PortalRow, Expect> expected, string[] forbiddenText)
    {
        var failures = new List<string>();
        var client = await fixture.SignInAsync(caller.Account.UserName);

        // Sign-out goes last: it ends the session every other row depends on.
        foreach (var row in PortalRows.OrderBy(r => r.Action == "Portal.Account.Logout POST"))
        {
            var expect = expected(row);
            if (expect == Expect.NotApplicable)
            {
                continue;
            }

            var label = $"[{caller.Description}] {row.Action} {row.Url(fixture)}";
            HttpResponseMessage response;
            if (row.Fields is null)
            {
                response = await client.GetAsync(row.Url(fixture));
            }
            else
            {
                // A genuine antiforgery token, from a page every signed-in caller reaches, so a
                // refusal is the access decision and never the antiforgery filter getting in first.
                var antiforgery = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Account/Settings");
                response = await client.PostAsync(row.Url(fixture), AdminPortalFactory.Form(antiforgery, row.Fields(fixture, caller)));
            }

            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync();
            var redirect = response.StatusCode == HttpStatusCode.Found ? RedirectPath(response) : null;
            var denied = redirect == "/Account/AccessDenied";

            void Fail(string why) => failures.Add($"{label}: expected {expect}, {why} (status {status}{(redirect is null ? "" : $" → {redirect}")})");

            if (status >= 500)
            {
                Fail("but the server errored");
                continue;
            }

            switch (expect)
            {
                case Expect.Denied when !denied:
                    Fail("but was not sent to /Account/AccessDenied");
                    break;
                case Expect.Refused when !denied && response.StatusCode != HttpStatusCode.OK:
                    Fail("but was neither denied nor shown the form again");
                    break;
                case Expect.NotFound when response.StatusCode != HttpStatusCode.NotFound:
                    Fail("but it was not a 404");
                    break;
                case Expect.NoRows or Expect.OwnRowsOnly when response.StatusCode != HttpStatusCode.OK:
                    Fail("but the screen did not open");
                    break;
                case Expect.OwnRowsOnly when row.OwnMarker is { } own && !body.Contains(own, StringComparison.Ordinal):
                    Fail($"but their own \"{own}\" was missing");
                    break;
                case Expect.Open when denied || redirect == "/Account/Login" && !row.Action.StartsWith("Portal.Account.", StringComparison.Ordinal):
                    Fail("but the caller was turned away");
                    break;
            }

            if (forbiddenText.FirstOrDefault(text => body.Contains(text, StringComparison.Ordinal)) is { } leaked)
            {
                Fail($"and the response showed \"{leaked}\"");
            }

            if (row.Written is not null && await fixture.ReadAsync(db => row.Written(fixture, caller, db)))
            {
                Fail("and the write went through");
            }
        }

        return failures;
    }

    private async Task<List<string>> RunApiAsync(HttpClient client, Caller caller, Func<ApiRow, Expect> expected)
    {
        var failures = new List<string>();

        foreach (var row in ApiRows)
        {
            var expect = expected(row);
            if (expect == Expect.NotApplicable)
            {
                continue;
            }

            var recordId = Guid.NewGuid();
            var label = $"[{caller.Description}] {row.Action} {row.Url(fixture)}";
            var response = row.Body is null
                ? await client.GetAsync(row.Url(fixture))
                : await client.PostAsJsonAsync(row.Url(fixture), row.Body(fixture, recordId));

            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync();

            void Fail(string why) => failures.Add($"{label}: expected {expect}, {why} (status {status}, body {Truncate(body)})");

            if (status >= 500)
            {
                Fail("but the server errored");
                continue;
            }

            switch (expect)
            {
                case Expect.NoRows when response.StatusCode != HttpStatusCode.OK || body.Trim() != "[]":
                    Fail("but it was not an empty list");
                    break;
                case Expect.NotFound when response.StatusCode != HttpStatusCode.NotFound:
                    Fail("but it was not a 404");
                    break;
                case Expect.NoContent when response.StatusCode != HttpStatusCode.NoContent:
                    Fail("but it was not a 204");
                    break;
                case Expect.BadRequest when response.StatusCode != HttpStatusCode.BadRequest:
                    Fail("but it was not a 400");
                    break;
                case Expect.GlobalLibrary when response.StatusCode != HttpStatusCode.OK:
                    Fail("but the library was not readable");
                    break;
                case Expect.Open when response.StatusCode is HttpStatusCode.Forbidden:
                    Fail("but the caller was forbidden");
                    break;
                case Expect.RefusedForLocation when LocationRefusal(response, body) != caller.LocationRefusal:
                    Fail($"but the refusal was not \"{caller.LocationRefusal}\"");
                    break;
            }

            // The customer and org names behind the scoped rows — a list endpoint answering with
            // them under any other status would be the same leak.
            if (expect != Expect.GlobalLibrary &&
                new[] { AccessAuditFixture.OwnCustomerName, AccessAuditFixture.ForeignCustomerName, AccessAuditFixture.CustomerPhone }
                    .FirstOrDefault(text => body.Contains(text, StringComparison.Ordinal)) is { } leaked)
            {
                Fail($"and the response showed \"{leaked}\"");
            }

            if (row.Written is not null && await fixture.ReadAsync(db => row.Written(recordId, db)))
            {
                Fail("and the record was written");
            }
        }

        return failures;
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private static void AssertNoFailures(List<string> failures) =>
        Assert.True(failures.Count == 0, $"{failures.Count} access audit failure(s):\n  " + string.Join("\n  ", failures));

    /// <summary>"{Portal|Api}.{Controller}.{Method} {VERB}". The C# method name rather than the
    /// route, so the two Login/Convert/SetPassword overloads are told apart by verb and the two
    /// ReferenceData controllers by surface. An action with no verb attribute answers GET here —
    /// MVC's conventional routing accepts any verb for it, and GET is how its screen is reached.</summary>
    private static string ActionKey(ControllerActionDescriptor action)
    {
        var surface = action.ControllerTypeInfo.Namespace!.Contains(".Api.", StringComparison.Ordinal) ? "Api" : "Portal";
        var verb = action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods).FirstOrDefault() ?? "GET";
        return $"{surface}.{action.ControllerName}.{action.MethodInfo.Name} {verb}";
    }

    private static string InvitedEmail(Caller caller) => $"invited-by-{caller.Account.UserId:N}@test.local";

    private async Task<LoginResponse> SignInToFieldAppAsync(AccessAuditFixture.Account account)
    {
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync(
            "api/v1/auth/login", new LoginRequest { UserName = account.UserName, Password = AccessControlFixture.Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private Task<CurrentLocation> OrgAsync(Guid orgNodeId) => fixture.ReadAsync(async db =>
    {
        var org = await db.OrganisationNodes.IgnoreQueryFilters().SingleAsync(o => o.Id == orgNodeId);
        return new CurrentLocation(org.Id, HierarchyPath.Parse(org.HierarchyPath), org.Name);
    });

    /// <summary>The reason a create endpoint gives for a location in this state — asked of
    /// CurrentLocationCheck itself rather than restated, so the audit pins <em>which</em> refusal
    /// is given without pinning its wording.</summary>
    private static string Refusal(CurrentLocationStatus status, CurrentLocation location) =>
        new CurrentLocationCheck(status, location).RefusalMessage!;

    /// <summary>The one form-level ("") message of a 400 ValidationProblemDetails, or a
    /// description of what came back instead.</summary>
    private static string LocationRefusal(HttpResponseMessage response, string body)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return $"<status {(int)response.StatusCode}>";
        }

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("errors", out var errors) ||
            !errors.TryGetProperty(string.Empty, out var formLevel) ||
            formLevel.GetArrayLength() != 1 ||
            errors.EnumerateObject().Count() != 1)
        {
            return "<not a single form-level refusal>";
        }

        return formLevel[0].GetString()!;
    }

    private static string? RedirectPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        return location is null
            ? null
            : (location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location)).AbsolutePath;
    }

    private static string Truncate(string body) => body.Length <= 200 ? body : body[..200] + "…";
}
