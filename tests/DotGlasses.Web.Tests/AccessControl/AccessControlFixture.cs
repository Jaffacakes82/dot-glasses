using System.Net;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// A small but complete org tree plus one signed-in-able portal account per (role, level)
/// combination the RBAC table distinguishes, so an access-control test can name a caller and a
/// target rather than building either.
///
/// Deliberately drives the <em>real Identity application cookie</em> rather than reusing
/// ticket 15's AdminPortalTestAuthenticationHandler. That handler is the right tool for the
/// screens it was built for, but it cannot serve this ticket: it derives from a plain
/// AuthenticationHandler&lt;&gt;, whose default HandleForbiddenAsync writes a bare 403. The
/// guarantee under test here — that a denied policy check lands the user on
/// /Account/AccessDenied rather than a dead end — is produced by the *cookie* handler's
/// AccessDeniedPath, so a test that swapped the scheme out would be asserting against its own
/// stand-in instead of the application. Accounts are therefore created through UserManager and
/// signed in through the real /Account/Login form, which means the cookie's own per-request
/// validation event loads each caller's scope, level and role from the database exactly as it
/// does in production.
///
/// The two static helpers from AdminPortalFactory (antiforgery scraping and form building) are
/// reused as-is — they take an HttpClient and are independent of how it was authenticated.
/// </summary>
public class AccessControlFixture : IAsyncLifetime
{
    /// <summary>Satisfies the tightened Identity policy in Program.cs (length 8, upper,
    /// non-alphanumeric), so account creation fails loudly on a policy change rather than
    /// silently leaving the fixture without callers.</summary>
    public const string Password = "TestPassw0rd!";

    // The DGI → Kenya → reseller → retail point spine already ships as seed data
    // (OrganisationSeedConfiguration), so it is reused rather than duplicated — a second node at
    // the same HierarchyPath is not a shape the application ever has to cope with. Only the two
    // nodes the deny paths need are added: a second country to be beside, and a sibling whose
    // path shares a prefix with the reseller's.
    //
    // "/1/2/30/" is the sibling-prefix landmine: it shares the characters "/1/2/3" with the
    // reseller at "/1/2/3/" and is kept out of that reseller's subtree only by the trailing
    // slash. Drop the slash anywhere in the chain and the reseller silently gains write access.
    public const string SiblingResellerPath = "/1/2/30/";
    public const string SecondCountryPath = "/1/5/";
    public const string SecondCountryRetailPointPath = "/1/5/50/";

    public const string DgiAdmin = "dgi-admin@test.local";
    public const string DgiUser = "dgi-user@test.local";
    public const string CountryAdmin = "kenya-admin@test.local";
    public const string CountryUser = "kenya-user@test.local";
    public const string ResellerAdmin = "reseller-admin@test.local";
    public const string OutletAdmin = "outlet-admin@test.local";

    // Accounts with more than one org assignment (ADR-0006). Each one's *active* org — the column
    // the old single-org model read scope from — is deliberately its lowest assignment, so a test
    // that passes for them cannot be passing on the active org alone.

    /// <summary>Nested assignments: DGI, and a retail point beneath it. Active org: the retail point.</summary>
    public const string DgiAndOutletAdmin = "dgi-and-outlet-admin@test.local";

    /// <summary>Assignments in separate trees: Kenya and Uganda. Active org: Kenya.</summary>
    public const string TwoCountriesAdmin = "two-countries-admin@test.local";

    /// <summary>A country (Uganda) and a retail point in a different country (Kenya's). Active
    /// org: the retail point.</summary>
    public const string CountryAndOutletAdmin = "country-and-outlet-admin@test.local";

    public CustomWebApplicationFactory Factory { get; } = new();

    /// <summary>Inside both the DGI and the Country Admin's subtree, and the reseller's own
    /// node — a legitimate target for an org write by any of them.</summary>
    public static Guid ResellerId => OrganisationSeedConfiguration.KenyaRetailerId;

    /// <summary>Outside the Country Admin's subtree entirely — beside them, not beneath.</summary>
    public Guid SecondCountryId { get; private set; }

    /// <summary>Outside the reseller's subtree, but only by the trailing slash.</summary>
    public Guid SiblingResellerId { get; private set; }

    /// <summary>A retail point in the second country — outside Kenya's subtree.</summary>
    public Guid SecondCountryRetailPointId { get; private set; }

    /// <summary>A user inside a Country Admin's subtree. Only ever used as a target, so no test
    /// can suspend an account another test needs to sign in with.</summary>
    public Guid InScopeTargetUserId { get; private set; }

    /// <summary>A user in the second country — outside a Kenya Admin's subtree.</summary>
    public Guid OutOfScopeTargetUserId { get; private set; }

    /// <summary>A user at the sibling reseller — outside the reseller Admin's subtree by the
    /// same one character that separates "/1/2/30/" from "/1/2/3/".</summary>
    public Guid SiblingResellerTargetUserId { get; private set; }

    public async Task InitializeAsync()
    {
        // Starts the throwaway Postgres container and applies the real migration chain. Without
        // this the factory's connection string points at an unstarted container.
        await Factory.InitializeAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in RoleNames.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var siblingReseller = Node(
            "Kangemi Vision Centre — Sibling", OrganisationLevel.Intermediate,
            SiblingResellerPath, OrganisationSeedConfiguration.KenyaId);
        var secondCountry = Node(
            "Uganda", OrganisationLevel.Country, SecondCountryPath, OrganisationSeedConfiguration.DgiId);

        var secondCountryRetailPoint = Node(
            "Kampala Outlet", OrganisationLevel.RetailPoint, SecondCountryRetailPointPath, secondCountry.Id);

        db.OrganisationNodes.AddRange(siblingReseller, secondCountry, secondCountryRetailPoint);
        await db.SaveChangesAsync();

        SiblingResellerId = siblingReseller.Id;
        SecondCountryId = secondCountry.Id;
        SecondCountryRetailPointId = secondCountryRetailPoint.Id;

        await CreateUserAsync(users, DgiAdmin, RoleNames.Admin, OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.DgiPath, OrganisationLevel.Dgi);
        await CreateUserAsync(users, DgiUser, RoleNames.User, OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.DgiPath, OrganisationLevel.Dgi);
        await CreateUserAsync(users, CountryAdmin, RoleNames.Admin, OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, OrganisationLevel.Country);
        await CreateUserAsync(users, CountryUser, RoleNames.User, OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, OrganisationLevel.Country);
        await CreateUserAsync(users, ResellerAdmin, RoleNames.Admin, OrganisationSeedConfiguration.KenyaRetailerId, OrganisationSeedConfiguration.KenyaRetailerPath, OrganisationLevel.Intermediate);
        await CreateUserAsync(users, OutletAdmin, RoleNames.Admin, OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint);

        await CreateUserAsync(users, DgiAndOutletAdmin, RoleNames.Admin,
            OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint,
            db, OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaRetailPointId);
        await CreateUserAsync(users, TwoCountriesAdmin, RoleNames.Admin,
            OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, OrganisationLevel.Country,
            db, OrganisationSeedConfiguration.KenyaId, secondCountry.Id);
        await CreateUserAsync(users, CountryAndOutletAdmin, RoleNames.Admin,
            OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint,
            db, secondCountry.Id, OrganisationSeedConfiguration.KenyaRetailPointId);

        InScopeTargetUserId = await CreateUserAsync(
            users, "outlet-target@test.local", RoleNames.User,
            OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint);
        OutOfScopeTargetUserId = await CreateUserAsync(
            users, "uganda-target@test.local", RoleNames.User,
            secondCountry.Id, secondCountry.HierarchyPath, OrganisationLevel.Country);
        SiblingResellerTargetUserId = await CreateUserAsync(
            users, "sibling-target@test.local", RoleNames.User,
            siblingReseller.Id, siblingReseller.HierarchyPath, OrganisationLevel.Intermediate);
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();

    /// <summary>Signs a portal account in through the real login form and returns a client
    /// holding its auth cookie. Redirects are not followed, so a denial's Location header — the
    /// evidence that a policy failure lands on the access-denied page — stays observable.</summary>
    public async Task<HttpClient> SignInAsync(string userName)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", AdminPortalFactory.Form(
            token, ("UserName", userName), ("Password", Password)));

        // A failed sign-in re-renders the login view with 200; only a success redirects. Asserted
        // here so a broken account surfaces as "sign-in failed" rather than as every policy
        // test mysteriously landing on /Account/Login.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return client;
    }

    /// <summary>Posts a portal form with a genuine antiforgery token minted for the signed-in
    /// caller. Needed wherever the resource-based check lives <em>inside</em> the action: the
    /// antiforgery filter would otherwise reject the post before the authorization decision
    /// under test was ever made.
    ///
    /// The token is scraped from /Organisations, which carries only [Authorize] and so is
    /// reachable by every caller in this fixture regardless of the policy under test — the token
    /// is bound to the caller's identity, not to the action it is posted to.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, params (string Key, string Value)[] fields)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");
        return await client.PostAsync(path, AdminPortalFactory.Form(token, fields));
    }

    /// <summary>Creates a throwaway account for a test that changes it (a role change, a removed
    /// assignment, a suspension), so no fixture-wide account is ever mutated. The first
    /// assignment doubles as the account's active org.</summary>
    public async Task<(string UserName, Guid UserId)> CreateAccountAsync(string role, params Guid[] assignedOrgIds)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var active = await db.OrganisationNodes.IgnoreQueryFilters().SingleAsync(o => o.Id == assignedOrgIds[0]);
        var userName = $"account-{Guid.NewGuid():N}@test.local";
        var userId = await CreateUserAsync(users, userName, role, active.Id, active.HierarchyPath, active.Level, db, assignedOrgIds);
        return (userName, userId);
    }

    private static OrganisationNode Node(string name, OrganisationLevel level, string path, Guid? parentId) => new()
    {
        Id = Guid.NewGuid(),
        ParentId = parentId,
        Name = name,
        Level = level,
        HierarchyPath = path,
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static async Task<Guid> CreateUserAsync(
        UserManager<ApplicationUser> users, string userName, string role,
        Guid orgNodeId, string hierarchyPath, OrganisationLevel orgLevel,
        DotGlassesDbContext? db = null, params Guid[] assignedOrgIds)
    {
        // The account's old active org (OrgNodeId/HierarchyPath/OrgLevel) is written exactly as
        // UserAdminService writes it. It counts as one of the account's org assignments until the
        // active-org columns are removed, so an account given no assignedOrgIds is scoped to it
        // alone; assignedOrgIds adds the rest.
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
            EmailConfirmed = true,
            FullName = userName,
            OrgNodeId = orgNodeId,
            HierarchyPath = hierarchyPath,
            OrgLevel = orgLevel,
        };

        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);

        if (assignedOrgIds.Length > 0)
        {
            db!.UserOrgAssignments.AddRange(assignedOrgIds.Select(orgId => new UserOrgAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OrgNodeId = orgId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            }));
            await db.SaveChangesAsync();
        }

        return user.Id;
    }
}
