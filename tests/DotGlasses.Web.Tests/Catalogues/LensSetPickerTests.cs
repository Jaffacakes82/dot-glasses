using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>
/// The Lens Sets screen follows prototype variant A's layout: one lens set on screen at a time,
/// chosen from a "Lens set" picker, with its lenses in one full-width table whose coatings and
/// pairings are chips. The chosen lens set is carried in the query string, so it survives every
/// POST-redirect-GET on the screen — and a refused lens, which renders the screen straight back.
/// </summary>
public class LensSetPickerTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;

    private static string P(decimal value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    private (Guid Id, string Name) SeedLensSet(string prefix)
    {
        var id = Guid.NewGuid();
        var name = $"{prefix} {id:N}";
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = id, Name = name, OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            LensSetTestData.AddSellableLens(db, id, label: "+1.00", sphere: 1.00m);
        });
        return (id, name);
    }

    /// <summary>The picker's chosen lens set: the id of its one selected option.</summary>
    private static Guid SelectedInPicker(string html)
    {
        var picker = Regex.Match(html, "<select[^>]*id=\"lensSetPicker\".*?</select>", RegexOptions.Singleline);
        Assert.True(picker.Success, "No lens set picker on the page.");
        var selected = Regex.Matches(picker.Value, "<option[^>]*value=\"([^\"]+)\"[^>]*\\sselected");
        return Guid.Parse(Assert.Single(selected).Groups[1].Value);
    }

    private static IReadOnlyList<string> LensTables(string html) =>
        Regex.Matches(html, "<table[^>]*aria-label=\"Lenses in ([^\"]*)\"").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();

    [Fact]
    public async Task WithSeveralLensSets_OneIsShownAtATime_AndThePickerSwitchesBetweenThem()
    {
        var first = SeedLensSet("Picker A");
        var second = SeedLensSet("Picker B");
        var third = SeedLensSet("Picker C");
        var client = factory.CreateAdminClient();

        foreach (var (id, name) in new[] { first, second, third })
        {
            var html = await client.GetStringAsync($"/Catalogues?catalogueId={id}");

            Assert.Equal(id, SelectedInPicker(html));
            Assert.Equal([name], LensTables(html));

            // Every lens set is still on offer in the picker.
            Assert.Contains($"value=\"{first.Id}\"", html);
            Assert.Contains($"value=\"{second.Id}\"", html);
            Assert.Contains($"value=\"{third.Id}\"", html);
        }
    }

    [Fact]
    public async Task WithNoLensSetAskedFor_OrOneThatIsNotThere_TheFirstIsShown()
    {
        SeedLensSet("Picker default");
        var client = factory.CreateAdminClient();

        var plain = await client.GetStringAsync("/Catalogues");
        var unknown = await client.GetStringAsync($"/Catalogues?catalogueId={Guid.NewGuid()}");

        Assert.Single(LensTables(plain));
        Assert.Equal(SelectedInPicker(plain), SelectedInPicker(unknown));
    }

    [Fact]
    public async Task TheSelectedLensSet_NamesItsOwner_ItsAssignedOrgs_AndItsLensCount()
    {
        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues?catalogueId={ExampleLensSets.SixLensSetId}");

        var meta = Regex.Match(html, "id=\"lensSetMeta\".*?<table", RegexOptions.Singleline);
        Assert.True(meta.Success, "No owner/assigned/lens-count line.");
        var text = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(meta.Value, "<[^>]+>", " ")), @"\s+", " ");
        Assert.Contains("Owned by", text);
        Assert.Matches(@"\d+ lens(es)?\b", text);
    }

    [Fact]
    public async Task CoatingsAndPairings_AreChips_AndNoPairingIsADash()
    {
        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues?catalogueId={ExampleLensSets.SixLensSetId}");

        Assert.Contains("<span class=\"dg-chip\">Photochromic</span>", html);
        Assert.Contains("<span class=\"dg-chip dg-chip-pair\">Blue block → Photochromic</span>", WebUtility.HtmlDecode(html));
        Assert.Contains("<span class=\"dg-muted\">—</span>", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task AfterAddingEditingOrRemovingALens_TheSameLensSetIsStillSelected()
    {
        // Not the first in the picker: the example sets and "Kept A…" both sort before it.
        SeedLensSet("Kept A");
        var (lensSetId, lensSetName) = SeedLensSet("Kept B");
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        Assert.NotEqual(lensSetId, SelectedInPicker(await client.GetStringAsync("/Catalogues")));

        // Add.
        var (_, afterAdd) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("Label", "+2.00"), ("Sphere", P(2.00m)), ("Cylinder", P(0m)),
            ("Add", P(0m)), ("CoatingIds", Clear.ToString())));
        Assert.Equal(lensSetId, SelectedInPicker(afterAdd));
        Assert.Equal([lensSetName], LensTables(afterAdd));
        Assert.Contains("+2.00", afterAdd.Replace("&#x2B;", "+"));

        // Edit.
        Guid lensId;
        using (var scope = factory.Services.CreateScope())
        {
            lensId = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().LensOptions
                .Single(l => l.PresetCatalogueId == lensSetId && l.Label == "+2.00").Id;
        }

        var (_, afterEdit) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("LensOptionId", lensId.ToString()), ("Label", "+2.25"), ("Sphere", P(2.25m)),
            ("Cylinder", P(0m)), ("Add", P(0m)), ("CoatingIds", Clear.ToString())));
        Assert.Equal(lensSetId, SelectedInPicker(afterEdit));

        // Remove.
        var (_, afterRemove) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RemoveLensOption",
            AdminPortalFactory.Form(token, ("lensOptionId", lensId.ToString())));
        Assert.Equal(lensSetId, SelectedInPicker(afterRemove));
        Assert.Equal([lensSetName], LensTables(afterRemove));
    }

    [Fact]
    public async Task ARefusedLens_ReopensTheDialog_OnTheLensSetItWasFor()
    {
        SeedLensSet("Refused A");
        var (lensSetId, lensSetName) = SeedLensSet("Refused B");
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        // No label and no coatings: refused, and rendered straight back rather than redirected.
        var response = await client.PostAsync("/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("Label", ""), ("Sphere", P(2.00m)), ("Cylinder", P(0m)), ("Add", P(0m))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-open-on-load=\"true\"", html);
        Assert.Equal(lensSetId, SelectedInPicker(html));
        Assert.Equal([lensSetName], LensTables(html));
    }

    [Fact]
    public async Task RenamingALensSet_AndUnassigningIt_KeepItSelected_AndRetiringItFallsBackToAnother()
    {
        SeedLensSet("Stay A");
        var (lensSetId, lensSetName) = SeedLensSet("Stay B");
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        var (_, afterAssign) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/AssignCatalogues", AdminPortalFactory.Form(token,
            ("selectedCatalogueId", lensSetId.ToString()),
            ("OrgNodeId", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()),
            ("CatalogueIds", lensSetId.ToString())));
        Assert.Equal(lensSetId, SelectedInPicker(afterAssign));
        Assert.Contains("/Catalogues/UnassignCatalogue", afterAssign);

        var (_, afterUnassign) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/UnassignCatalogue", AdminPortalFactory.Form(token,
            ("catalogueId", lensSetId.ToString()), ("orgNodeId", OrganisationSeedConfiguration.KenyaRetailPointId.ToString())));
        Assert.Equal(lensSetId, SelectedInPicker(afterUnassign));
        Assert.Contains("Not assigned to any org yet", afterUnassign);

        var (_, afterRename) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/UpdateCatalogue", AdminPortalFactory.Form(token,
            ("Id", lensSetId.ToString()), ("Name", $"{lensSetName} renamed")));
        Assert.Equal(lensSetId, SelectedInPicker(afterRename));
        Assert.Equal([$"{lensSetName} renamed"], LensTables(afterRename));

        var (_, afterRetire) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RetireCatalogue",
            AdminPortalFactory.Form(token, ("catalogueId", lensSetId.ToString())));
        Assert.NotEqual(lensSetId, SelectedInPicker(afterRetire));

        var (_, afterReactivate) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/ReactivateCatalogue",
            AdminPortalFactory.Form(token, ("catalogueId", lensSetId.ToString())));
        Assert.Equal(lensSetId, SelectedInPicker(afterReactivate));
    }

    [Fact]
    public async Task ASearch_NarrowsThePicker_AndASearchThatMatchesNothingSaysSo()
    {
        var (lensSetId, lensSetName) = SeedLensSet("Searchable");
        var client = factory.CreateAdminClient();

        var found = await client.GetStringAsync($"/Catalogues?search={Uri.EscapeDataString(lensSetName)}");
        Assert.Equal(lensSetId, SelectedInPicker(found));
        Assert.DoesNotContain($"value=\"{ExampleLensSets.SixLensSetId}\"", found);

        var none = await client.GetStringAsync($"/Catalogues?search={Guid.NewGuid():N}");
        Assert.Contains("No lens sets match", none);
        Assert.Empty(LensTables(none));
    }

    [Fact]
    public async Task ALensSetWithNoLenses_HasAnEmptyState()
    {
        var id = Guid.NewGuid();
        factory.Seed(db => db.PresetCatalogues.Add(new PresetCatalogue { Id = id, Name = $"Empty {id:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId }));

        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues?catalogueId={id}");

        Assert.Equal(id, SelectedInPicker(html));
        Assert.Contains("No lenses yet. Add one to start.", html);
        Assert.Empty(LensTables(html));
    }

    [Fact]
    public async Task LensSetsAndLensPowers_AreTwoTabs_ThatCarryTheSelectedLensSet()
    {
        var (lensSetId, _) = SeedLensSet("Tabbed");
        var client = factory.CreateAdminClient();

        var lensSets = await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}");
        Assert.Matches($"<a class=\"dg-tab active\"[^>]*href=\"/Catalogues\\?catalogueId={lensSetId}\"[^>]*>Lens sets</a>", lensSets);
        Assert.Matches($"<a class=\"dg-tab \"[^>]*href=\"/Catalogues/LensPowers\\?catalogueId={lensSetId}\"[^>]*>Lens powers</a>", lensSets);

        var lensPowers = await client.GetStringAsync($"/Catalogues/LensPowers?catalogueId={lensSetId}");
        Assert.Matches($"<a class=\"dg-tab \"[^>]*href=\"/Catalogues\\?catalogueId={lensSetId}\"[^>]*>Lens sets</a>", lensPowers);
        Assert.Matches("<a class=\"dg-tab active\"[^>]*>Lens powers</a>", lensPowers);
    }
}
