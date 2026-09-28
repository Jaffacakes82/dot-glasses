using System.Net;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.ReferenceData;

/// <summary>
/// ADR-0007 retired the "Lens strength" list (a lens set lens is a lens power now, with its own
/// label) and moved coating pairings onto each lens set lens. Reference Data shows neither any
/// more; coating exclusions stay, global.
/// </summary>
public class ReferenceDataScreenTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task NeitherLensStrengthsNorPairingsAreShown_ButExclusionsStay()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/ReferenceData");

        Assert.DoesNotContain("Lens strength", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pairing", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AddCoatingPairing", html);

        Assert.Contains("Coatings &amp; tints", html);
        Assert.Contains("Exclusions (can never be selected together)", html);
        Assert.Contains("action=\"/ReferenceData/AddCoatingExclusion\"", html);
        Assert.Contains("Lens types", html);
    }

    [Theory]
    [InlineData("/ReferenceData/AddCoatingPairing")]
    [InlineData("/ReferenceData/RemoveCoatingPairing")]
    public async Task TheGlobalPairingActionsNoLongerExist(string path)
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/ReferenceData");

        var response = await client.PostAsync(path, AdminPortalFactory.Form(token, ("id", Guid.NewGuid().ToString())));

        // Nothing handles it: routing answers 404 or 405 depending on which other route the
        // path happens to resemble, and either way it is neither a success nor a redirect.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    /// <summary>The category's number is retired, not reused: a POST naming it — a page still
    /// showing the old Lens strengths card — is a category that doesn't exist, and nothing is
    /// created. In particular it must not land in Occupations, which is what MVC binds an
    /// undefined enum number to (its default) when only the validator is consulted.</summary>
    [Fact]
    public async Task CreatingAnItemInTheRetiredLensStrengthCategory_IsRefused()
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/ReferenceData");
        var label = $"+9.75 {Guid.NewGuid():N}";

        var response = await client.PostAsync("/ReferenceData/Create", AdminPortalFactory.Form(token, ("Category", "6"), ("Label", label)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The value &#x27;6&#x27; is invalid.", await response.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        Assert.False(await db.ReferenceDataItems.AnyAsync(x => x.Label == label));
    }
}
