using System.Net;
using System.Text.RegularExpressions;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>
/// Since ADR-0007 a lens set lens is a lens power with its own coatings and pairings, so the Lens
/// Sets screen lists the selected set's lenses in a table — label, lens power, lens type, coatings and
/// pairings — and the global "Lens strength coating availability" grid, its save action and the
/// old add-a-lens-strength picker are gone (the Add lens dialog is lens-power ticket 07).
/// </summary>
public class LensSetLensTableTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    /// <summary>The page's text with tags stripped and entities decoded, whitespace collapsed —
    /// what an admin reads, so a row can be asserted as one line.</summary>
    private static string VisibleText(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ");

    /// <summary>The selected lens set's part of the screen: from the first time the page names it
    /// (the picker) to its Edit dialog, which the view renders straight after its lenses card.</summary>
    private static string CardFor(string html, string lensSetName)
    {
        var text = VisibleText(html);
        var start = text.IndexOf(lensSetName, StringComparison.Ordinal);
        var end = text.IndexOf($"Edit {lensSetName}", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"No card for {lensSetName}.");
        return text[start..end];
    }

    [Fact]
    public async Task EachLensShowsItsLabel_LensPower_LensType_Coatings_AndPairings()
    {
        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues?catalogueId={ExampleLensSets.SixLensSetId}");

        Assert.Contains($"aria-label=\"Lenses in 6-Lens Set\"", html);
        var text = VisibleText(html);
        Assert.Contains("Label Lens power Lens type Coatings Pairings", text);

        // The example 6-Lens +2.50: single vision, three coatings in the Coating list's own order
        // (one chip each), and the example pairing as one "trigger → paired" chip.
        Assert.Contains("+2.50 SPH +2.50 Single vision Photochromic Clear Blue block Blue block → Photochromic", text);

        // A bifocal: its add, its lens type, Photochromic only, no pairing.
        Assert.Contains("Bifocal +2.50 SPH 0.00 · ADD +2.50 Bifocal Photochromic —", text);
    }

    [Fact]
    public async Task TheGlobalCoatingGrid_AndTheLensStrengthPicker_AreGone()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues");

        Assert.DoesNotContain("coating availability", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lens strength", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SaveCoatingAvailability", html);
        Assert.DoesNotContain("AddLensOption", html);
        Assert.DoesNotContain("name=\"Selected\"", html);
        Assert.DoesNotContain("name=\"LensStrengthRefId\"", html);
    }

    [Theory]
    [InlineData("/Catalogues/SaveCoatingAvailability")]
    [InlineData("/Catalogues/AddLensOption")]
    public async Task TheRemovedActionsNoLongerExist(string path)
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        var response = await client.PostAsync(path, AdminPortalFactory.Form(token, ("Selected", "x")));

        // Nothing handles it: routing answers 404 or 405 depending on which other route the
        // path happens to resemble, and either way it is neither a success nor a redirect.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task RemovingALens_TakesItOutOfTheTable()
    {
        // A lens set of its own, so the shared example sets stay intact for every other test.
        var lensSetId = Guid.NewGuid();
        var lensSetName = $"Removal Readers {lensSetId:N}";
        Guid lensId = default;
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new Domain.Entities.PresetCatalogue { Id = lensSetId, Name = lensSetName, OwningOrgNodeId = Infrastructure.Persistence.Configurations.OrganisationSeedConfiguration.DgiId });
            (lensId, _) = LensSetTestData.AddSellableLens(db, lensSetId, label: "-1.75", sphere: -1.75m);
            LensSetTestData.AddSellableLens(db, lensSetId, label: "+0.75", sphere: 0.75m);
        });
        var client = factory.CreateAdminClient();
        Assert.Contains("-1.75 SPH -1.75", CardFor(await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}"), lensSetName));

        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        var (_, html) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RemoveLensOption",
            AdminPortalFactory.Form(token, ("lensOptionId", lensId.ToString())));

        var card = CardFor(html, lensSetName);
        Assert.DoesNotContain("-1.75", card);
        Assert.Contains("+0.75 SPH +0.75", card);
    }
}
