using System.Net;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// One test per access-control guarantee in CLAUDE.md's RBAC table, each exercising the allow
/// side and — the point of the exercise — every deny side that guarantee is supposed to cover.
/// A regression here is a data-exposure incident rather than a wrong number on a screen, which
/// is why this sits above the coverage bar (spec, Testing Decisions).
///
/// The guarantees are stated in terms of who reaches a screen or completes a write, never in
/// terms of which requirement type or handler produced the decision — the policies could be
/// re-implemented wholesale and these tests should still hold.
///
/// WidgetExample.Create is the one row of that table not covered here: WidgetExamplesApiTests
/// already exercises both its sides (Admin creates, plain User is refused) over the JWT API it
/// gates, and it carries no level or scope check to add a boundary case to.
/// </summary>
public class AccessControlPolicyTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    [Fact]
    public async Task ReferenceDataManagement_IsReachable_OnlyByAnAdminAtTheTopOfTheHierarchy()
    {
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        (await dgiAdmin.GetAsync("/ReferenceData")).EnsureSuccessStatusCode();

        // One level down is one level too far, Admin or not.
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);
        AssertAccessDenied(await countryAdmin.GetAsync("/ReferenceData"));

        // At the top of the hierarchy, but not an Admin — proves the role half is load-bearing
        // too, not just the level half.
        var dgiUser = await fixture.SignInAsync(AccessControlFixture.DgiUser);
        AssertAccessDenied(await dgiUser.GetAsync("/ReferenceData"));
    }

    [Fact]
    public async Task PresetCatalogueManagement_IsReachable_OnlyByAnAdminAtCountryLevelOrAbove()
    {
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);
        (await countryAdmin.GetAsync("/Catalogues")).EnsureSuccessStatusCode();

        // "Or above" is a real part of the rule, so the level above Country is exercised too.
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        (await dgiAdmin.GetAsync("/Catalogues")).EnsureSuccessStatusCode();

        // Below Country: an Admin at a reseller cannot manage catalogues.
        var resellerAdmin = await fixture.SignInAsync(AccessControlFixture.ResellerAdmin);
        AssertAccessDenied(await resellerAdmin.GetAsync("/Catalogues"));

        // At Country, but not an Admin — unlike Custom Orders, this one is role-gated too.
        var countryUser = await fixture.SignInAsync(AccessControlFixture.CountryUser);
        AssertAccessDenied(await countryUser.GetAsync("/Catalogues"));
    }

    [Fact]
    public async Task CustomOrdersScreen_IsReachable_ByAnyRoleAtCountryLevelOrAbove()
    {
        // "Any role" is the half of this rule most likely to be broken by someone tightening the
        // policy to match its neighbours, so the allow case is deliberately a plain User...
        var countryUser = await fixture.SignInAsync(AccessControlFixture.CountryUser);
        (await countryUser.GetAsync("/CustomOrders")).EnsureSuccessStatusCode();

        // ...and both deny cases are Admins, to prove the level check is doing the work.
        var resellerAdmin = await fixture.SignInAsync(AccessControlFixture.ResellerAdmin);
        AssertAccessDenied(await resellerAdmin.GetAsync("/CustomOrders"));

        var outletAdmin = await fixture.SignInAsync(AccessControlFixture.OutletAdmin);
        AssertAccessDenied(await outletAdmin.GetAsync("/CustomOrders"));
    }

    [Fact]
    public async Task CustomOrdersAdvanceAction_IsGatedByTheSameLevelRuleAsTheScreen()
    {
        // The write action is a separate endpoint from the screen, so hiding the screen is not
        // by itself evidence the action is gated — a caller below Country could otherwise post
        // to it directly. Both callers post a genuine antiforgery token and an unknown saleId,
        // so the only difference between the two outcomes is the policy.
        var outletAdmin = await fixture.SignInAsync(AccessControlFixture.OutletAdmin);
        AssertAccessDenied(await AccessControlFixture.PostFormAsync(
            outletAdmin, "/CustomOrders/AdvanceStatus", ("saleId", Guid.NewGuid().ToString())));

        // A Country-level caller gets past the policy and into the action body, where the
        // unknown saleId is rejected as a domain rule violation and redirected back to the
        // screen. Landing anywhere other than /Account/AccessDenied is the point.
        var countryUser = await fixture.SignInAsync(AccessControlFixture.CountryUser);
        var pastThePolicy = await AccessControlFixture.PostFormAsync(
            countryUser, "/CustomOrders/AdvanceStatus", ("saleId", Guid.NewGuid().ToString()));

        AssertRedirectedTo("/CustomOrders", pastThePolicy);
    }

    [Fact]
    public async Task OrganisationWrite_IsRefused_WhenTheTargetOrgIsOutsideTheCallersSubtree()
    {
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        // Inside the caller's own subtree: the write completes and redirects back to the screen.
        var allowed = await AccessControlFixture.PostFormAsync(
            countryAdmin, "/Organisations/SetTrainingOrgFlag",
            ("id", AccessControlFixture.ResellerId.ToString()), ("value", "true"));
        AssertRedirectedTo("/Organisations", allowed);

        // A different country — beside the caller, not beneath them.
        var outside = await AccessControlFixture.PostFormAsync(
            countryAdmin, "/Organisations/SetTrainingOrgFlag",
            ("id", fixture.SecondCountryId.ToString()), ("value", "true"));
        AssertAccessDenied(outside);

        // The same write, by the DGI Admin above both countries, succeeds — so the refusal above
        // is the scope rule and not a missing or unwritable target node.
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        var fromAbove = await AccessControlFixture.PostFormAsync(
            dgiAdmin, "/Organisations/SetTrainingOrgFlag",
            ("id", fixture.SecondCountryId.ToString()), ("value", "true"));
        AssertRedirectedTo("/Organisations", fromAbove);
    }

    [Fact]
    public async Task OrganisationWrite_IsRefused_WhenTheTargetOrgMerelySharesAPathPrefix()
    {
        // "/1/2/30/" starts with the characters "/1/2/3" but is not beneath "/1/2/3/". This is
        // the boundary the trailing-slash invariant exists for, and the case a naive
        // StartsWith on an unslashed prefix would silently allow.

        // First prove the sibling is a real, writable node, by writing to it as the Country
        // Admin above it. Without this the test would still pass if the node had never been
        // created — a missing target is refused by the same code path as an out-of-scope one,
        // so "denied" on its own is not evidence the prefix rule did the work.
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);
        var byAnAncestor = await AccessControlFixture.PostFormAsync(
            countryAdmin, "/Organisations/SetTrainingOrgFlag",
            ("id", fixture.SiblingResellerId.ToString()), ("value", "true"));
        AssertRedirectedTo("/Organisations", byAnAncestor);

        var resellerAdmin = await fixture.SignInAsync(AccessControlFixture.ResellerAdmin);

        var ownNode = await AccessControlFixture.PostFormAsync(
            resellerAdmin, "/Organisations/SetTrainingOrgFlag",
            ("id", AccessControlFixture.ResellerId.ToString()), ("value", "false"));
        AssertRedirectedTo("/Organisations", ownNode);

        // Same node, same action, one character of hierarchy path between allowed and refused.
        var sibling = await AccessControlFixture.PostFormAsync(
            resellerAdmin, "/Organisations/SetTrainingOrgFlag",
            ("id", fixture.SiblingResellerId.ToString()), ("value", "false"));
        AssertAccessDenied(sibling);
    }

    [Fact]
    public async Task OrganisationWrite_IsRefused_ForANonAdmin_EvenInsideTheirOwnSubtree()
    {
        // Scope and role are independent halves of the same policy: being in scope is not
        // enough on its own.
        var countryUser = await fixture.SignInAsync(AccessControlFixture.CountryUser);

        var response = await AccessControlFixture.PostFormAsync(
            countryUser, "/Organisations/SetTrainingOrgFlag",
            ("id", AccessControlFixture.ResellerId.ToString()), ("value", "true"));

        AssertAccessDenied(response);
    }

    [Fact]
    public async Task UserDirectoryWrite_IsRefused_WhenTheTargetUserIsOutsideTheCallersSubtree()
    {
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        var allowed = await AccessControlFixture.PostFormAsync(
            countryAdmin, "/UserDirectory/Suspend", ("id", fixture.InScopeTargetUserId.ToString()));
        AssertRedirectedTo("/UserDirectory", allowed);

        // A user assigned to another country. Suspending them must not be possible.
        var outside = await AccessControlFixture.PostFormAsync(
            countryAdmin, "/UserDirectory/Suspend", ("id", fixture.OutOfScopeTargetUserId.ToString()));
        AssertAccessDenied(outside);

        // The same suspension, by the DGI Admin above both countries, succeeds — so the refusal
        // above is the scope rule and not a missing or unsuspendable target account.
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        var fromAbove = await AccessControlFixture.PostFormAsync(
            dgiAdmin, "/UserDirectory/Suspend", ("id", fixture.OutOfScopeTargetUserId.ToString()));
        AssertRedirectedTo("/UserDirectory", fromAbove);
    }

    [Fact]
    public async Task UserDirectoryWrite_IsRefused_WhenTheTargetUserMerelySharesAPathPrefix()
    {
        // The same sibling-prefix boundary as the org write, on the other resource-based policy —
        // the two resolve their target through different services, so one being correct is not
        // evidence about the other.

        // As above: prove the target user is real and suspendable by an Admin above them, so a
        // missing account cannot masquerade as a scope refusal.
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);
        var byAnAncestor = await AccessControlFixture.PostFormAsync(
            countryAdmin, "/UserDirectory/Suspend", ("id", fixture.SiblingResellerTargetUserId.ToString()));
        AssertRedirectedTo("/UserDirectory", byAnAncestor);

        var resellerAdmin = await fixture.SignInAsync(AccessControlFixture.ResellerAdmin);

        var ownSubtree = await AccessControlFixture.PostFormAsync(
            resellerAdmin, "/UserDirectory/Suspend", ("id", fixture.InScopeTargetUserId.ToString()));
        AssertRedirectedTo("/UserDirectory", ownSubtree);

        var sibling = await AccessControlFixture.PostFormAsync(
            resellerAdmin, "/UserDirectory/Suspend", ("id", fixture.SiblingResellerTargetUserId.ToString()));
        AssertAccessDenied(sibling);
    }

    [Fact]
    public async Task UserDirectoryWrite_IsRefused_ForANonAdmin_EvenInsideTheirOwnSubtree()
    {
        var countryUser = await fixture.SignInAsync(AccessControlFixture.CountryUser);

        var response = await AccessControlFixture.PostFormAsync(
            countryUser, "/UserDirectory/Suspend", ("id", fixture.InScopeTargetUserId.ToString()));

        AssertAccessDenied(response);
    }

    [Fact]
    public async Task ADeniedPolicyCheck_LandsOnARealAccessDeniedPage_NotABareNotFound()
    {
        // CLAUDE.md claims /Account/AccessDenied is a real page rather than a 404, and that is
        // exactly the kind of claim that rots. Following the redirect is the only way it stays
        // honest: the Location header alone would still pass if the page behind it were deleted.
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        var denial = await countryAdmin.GetAsync("/ReferenceData");
        AssertAccessDenied(denial);

        var page = await countryAdmin.GetAsync(denial.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains(
            "You don't have permission to view this page",
            await page.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Asserts the response is the cookie handler's access-denied redirect. That the
    /// page behind it actually renders is asserted once, by
    /// ADeniedPolicyCheck_LandsOnARealAccessDeniedPage_NotABareNotFound — repeating the fetch in
    /// every test would cost a round trip per assertion to re-prove one global fact.</summary>
    private static void AssertAccessDenied(HttpResponseMessage response) =>
        AssertRedirectedTo("/Account/AccessDenied", response);

    private static void AssertRedirectedTo(string path, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(path, RedirectPath(response), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The cookie handler's access-denied redirect is absolute while a controller's own
    /// RedirectToAction is relative, so both are normalised to a path before comparison. The
    /// query string is dropped: ReturnUrl and selectedId are not part of any guarantee here.</summary>
    private static string RedirectPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return (location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location)).AbsolutePath;
    }
}
