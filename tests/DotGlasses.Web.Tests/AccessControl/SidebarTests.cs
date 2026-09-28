using DotGlasses.Application.Common;
using DotGlasses.Infrastructure.Persistence.Configurations;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// Ticket 05: the Admin Portal sidebar footer shows the signed-in user's role and the names of
/// every org they're assigned to, sorted by level then name and capped at "+N more" past a small
/// limit. Drives the layout the same way CombinedScopeTests does — a real signed-in cookie client
/// requesting a real page (Home/Index, which carries only [Authorize] so every seeded role/level
/// reaches it) and asserting on the rendered HTML, never on CurrentUserContext/service internals.
///
/// The email-truncation fix (spec.md user story 9) is pure CSS (overflow/text-overflow/ellipsis
/// on .dg-user-chip-email, min-width:0 on its flex parent .dg-user-chip-info) and isn't testable
/// from a rendered-HTML assertion — see dot-glasses.css and this ticket's Comments section for
/// what was applied and why.
/// </summary>
public class SidebarTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    [Fact]
    public async Task AUserWithOneAssignment_SeesItsRoleAndOneAssignmentName_NoMoreLink()
    {
        // DgiAdmin (AccessControlFixture) is seeded with a single assignment: the DGI root node.
        var client = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var html = await client.GetStringAsync("/");

        Assert.Contains("Admin", html);
        Assert.Contains("DOT Glasses International", html);
        Assert.DoesNotMatch(@"(\+|&#x2B;)\d+ more", html);
    }

    [Fact]
    public async Task AUserWithMoreAssignmentsThanTheCap_SeesTheCapAndAPlusNMoreCount()
    {
        // Four distinct assignment rows (nesting is irrelevant here — the sidebar lists raw
        // assignments, not the deduped scope roots ADR-0006 builds for authorization), sorted by
        // level then name: DGI, Kenya, Kangemi Vision Centre, then the outreach post retail point.
        // With a cap of 3 the retail point must be folded into "+1 more" rather than shown.
        var (userName, _) = await fixture.CreateAccountAsync(
            RoleNames.Admin,
            OrganisationSeedConfiguration.DgiId,
            OrganisationSeedConfiguration.KenyaId,
            OrganisationSeedConfiguration.KenyaRetailerId,
            OrganisationSeedConfiguration.KenyaRetailPointId);
        var client = await fixture.SignInAsync(userName);

        var html = await client.GetStringAsync("/");

        Assert.Contains("DOT Glasses International", html);
        Assert.Contains("Kenya", html);
        Assert.Contains("Kangemi Vision Centre", html);
        // Razor HTML-encodes "+" as "&#x2B;" in rendered text content, so match on the digit and
        // word rather than the literal character.
        Assert.Matches(@"(\+|&#x2B;)1 more", html);
        // The fourth (lowest-level) assignment is folded away rather than rendered.
        Assert.DoesNotContain("Outreach Post", html);
    }
}
