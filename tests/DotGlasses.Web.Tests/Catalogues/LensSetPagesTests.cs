using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>
/// Lens Sets is two pages: a list with one row per lens set (filtered by status and name), and a
/// page per lens set where its lenses are configured and it is assigned to organisations. Every
/// write made from a lens set's page comes back to that page.
/// </summary>
public class LensSetPagesTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;
    private static readonly Guid KenyaRetailPoint = OrganisationSeedConfiguration.KenyaRetailPointId;

    private static string P(decimal value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    private static string Text(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    private (Guid Id, string Name) SeedLensSet(string prefix, bool withLens = true, bool retired = false, string? description = null)
    {
        var id = Guid.NewGuid();
        var name = $"{prefix} {id:N}";
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue
            {
                Id = id, Name = name, Description = description, OwningOrgNodeId = OrganisationSeedConfiguration.DgiId, IsDeleted = retired,
            });
            if (withLens)
            {
                LensSetTestData.AddSellableLens(db, id, label: "+1.00", sphere: 1.00m);
            }
        });
        return (id, name);
    }

    /// <summary>The visible text of a lens set's row in the list, or null if it has none.</summary>
    private static string? Row(string html, Guid lensSetId)
    {
        var row = Regex.Match(html, $"<tr data-lens-set-id=\"{lensSetId}\">.*?</tr>", RegexOptions.Singleline);
        return row.Success ? Text(row.Value) : null;
    }

    private static string Section(string html, string id)
    {
        var section = Regex.Match(html, $"<div class=\"dg-card\" id=\"{id}\".*?(?=<div class=\"dg-card\"|<div class=\"modal|$)", RegexOptions.Singleline);
        Assert.True(section.Success, $"No {id} section on the page.");
        return section.Value;
    }

    private static string? RedirectPath(HttpResponseMessage response) => response.Headers.Location?.OriginalString;

    // --- The list ------------------------------------------------------------------------------

    [Fact]
    public async Task TheList_HasARowPerLensSet_WithItsOwnerLensCountAndAssignments_AndNoPicker()
    {
        var (withLens, withLensName) = SeedLensSet("Listed", description: "Stock readers");
        var (empty, _) = SeedLensSet("Listed empty", withLens: false);
        factory.Seed(db => db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment
        {
            Id = Guid.NewGuid(), PresetCatalogueId = withLens, OrgNodeId = KenyaRetailPoint, CreatedAtUtc = DateTimeOffset.UtcNow,
        }));

        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues");

        var row = Row(html, withLens);
        Assert.NotNull(row);
        Assert.Contains(withLensName, row);
        Assert.Contains("Stock readers", row);
        Assert.Contains("1 lens", row);
        Assert.Contains("1 organisation", row);
        Assert.Contains("Active", row);
        Assert.Contains($"href=\"/Catalogues/Details/{withLens}\"", html);

        var emptyRow = Row(html, empty);
        Assert.NotNull(emptyRow);
        Assert.Contains("No lenses yet", emptyRow);
        Assert.Contains("Not assigned", emptyRow);

        Assert.DoesNotContain("lensSetPicker", html);
        Assert.DoesNotContain("Assign lens sets to a retailer", html);
        Assert.DoesNotContain("Lenses in ", html);
    }

    [Fact]
    public async Task TheStatusFilter_OpensOnActive_AndRetiredAndAllShowRetiredLensSets()
    {
        var (active, _) = SeedLensSet("Status active");
        var (retired, _) = SeedLensSet("Status retired", retired: true);
        var client = factory.CreateAdminClient();

        var byDefault = await client.GetStringAsync("/Catalogues");
        Assert.NotNull(Row(byDefault, active));
        Assert.Null(Row(byDefault, retired));
        Assert.Matches("<option value=\"active\" selected", byDefault);

        var onlyRetired = await client.GetStringAsync("/Catalogues?status=retired");
        Assert.Null(Row(onlyRetired, active));
        var retiredRow = Row(onlyRetired, retired);
        Assert.NotNull(retiredRow);
        Assert.Contains("Retired", retiredRow);
        Assert.Contains("Reactivate", retiredRow);

        var all = await client.GetStringAsync("/Catalogues?status=all");
        Assert.NotNull(Row(all, active));
        Assert.NotNull(Row(all, retired));

        // Anything unrecognised is the default, not an error.
        Assert.Null(Row(await client.GetStringAsync("/Catalogues?status=nonsense"), retired));
    }

    [Fact]
    public async Task TheNameSearch_FiltersWithinTheChosenStatus_AndSaysWhenNothingMatches()
    {
        var (active, activeName) = SeedLensSet("Searchable");
        var (retired, retiredName) = SeedLensSet("Searchable retired", retired: true);
        var client = factory.CreateAdminClient();

        var found = await client.GetStringAsync($"/Catalogues?search={Uri.EscapeDataString(activeName)}");
        Assert.NotNull(Row(found, active));
        Assert.Null(Row(found, ExampleLensSets.SixLensSetId));

        // A retired lens set's name finds nothing among the active ones…
        Assert.Contains("No lens sets match", await client.GetStringAsync($"/Catalogues?search={Uri.EscapeDataString(retiredName)}"));
        // …and finds it once the status filter includes it.
        Assert.NotNull(Row(await client.GetStringAsync($"/Catalogues?status=retired&search={Uri.EscapeDataString(retiredName)}"), retired));
    }

    [Fact]
    public async Task TheTabs_AreOnTheListAndLensPowers_ButNotOnALensSetsPage()
    {
        var (lensSetId, lensSetName) = SeedLensSet("Tabbed");
        var client = factory.CreateAdminClient();

        var list = await client.GetStringAsync("/Catalogues");
        Assert.Matches("<a class=\"dg-tab active\"[^>]*href=\"/Catalogues\"[^>]*>Lens sets</a>", list);
        Assert.Matches("<a class=\"dg-tab \"[^>]*href=\"/Catalogues/LensPowers\"[^>]*>Lens powers</a>", list);

        var lensPowers = await client.GetStringAsync("/Catalogues/LensPowers");
        Assert.Matches("<a class=\"dg-tab \"[^>]*href=\"/Catalogues\"[^>]*>Lens sets</a>", lensPowers);
        Assert.Matches("<a class=\"dg-tab active\"[^>]*>Lens powers</a>", lensPowers);

        var page = await client.GetStringAsync($"/Catalogues/Details/{lensSetId}");
        Assert.DoesNotContain("dg-tabs", page);
        var crumb = Regex.Match(page, "<nav class=\"dg-crumb\".*?</nav>", RegexOptions.Singleline);
        Assert.True(crumb.Success, "No breadcrumb on the lens set's page.");
        Assert.Contains("href=\"/Catalogues\"", crumb.Value);
        Assert.Equal($"Lens Sets › {lensSetName}", Text(crumb.Value));
    }

    // --- A lens set's page -----------------------------------------------------------------------

    [Fact]
    public async Task ALensSetsPage_ShowsItsOwnerItsLensesAndWhereItIsAssigned()
    {
        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues/Details/{ExampleLensSets.SixLensSetId}");

        var meta = Text(Section(html, "lensSetMeta"));
        Assert.Contains("Owned by", meta);
        Assert.Matches(@"\d+ lens(es)?\b", meta);

        Assert.Single(Regex.Matches(html, "<table[^>]*aria-label=\"Lenses in "));
        Assert.Contains("<span class=\"dg-chip\">Photochromic</span>", html);
        Assert.Contains("<span class=\"dg-chip dg-chip-pair\">Blue block → Photochromic</span>", WebUtility.HtmlDecode(html));
        Assert.Contains("<span class=\"dg-muted\">—</span>", WebUtility.HtmlDecode(html));

        Assert.Contains("Assigned to", Text(Section(html, "lensSetAssignments")));
    }

    [Fact]
    public async Task ALensSetThatDoesNotExist_GoesBackToTheList()
    {
        var response = await factory.CreateAdminClient().GetAsync($"/Catalogues/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Catalogues", RedirectPath(response));
    }

    [Fact]
    public async Task ALensSetWithNoLenses_HasAnEmptyState()
    {
        var (id, _) = SeedLensSet("Empty", withLens: false);

        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues/Details/{id}");

        Assert.Contains("No lenses yet. Add one to start.", html);
        Assert.DoesNotContain("aria-label=\"Lenses in ", html);
    }

    [Fact]
    public async Task CreatingALensSet_OpensItsPage_AndRetiringItReturnsToTheList()
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        var name = $"Created {Guid.NewGuid():N}";

        var (created, page) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/CreateCatalogue", AdminPortalFactory.Form(token, ("Name", name)));

        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            id = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().PresetCatalogues.Single(c => c.Name == name).Id;
        }
        Assert.Equal($"/Catalogues/Details/{id}", RedirectPath(created));
        Assert.Contains("No lenses yet. Add one to start.", page);

        var (retired, list) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RetireCatalogue",
            AdminPortalFactory.Form(token, ("catalogueId", id.ToString())));
        Assert.Equal("/Catalogues", RedirectPath(retired));
        Assert.Null(Row(list, id));
    }

    [Fact]
    public async Task AddingEditingAndRemovingALens_AndRenaming_AllComeBackToTheLensSetsPage()
    {
        var (lensSetId, lensSetName) = SeedLensSet("Kept");
        var page = $"/Catalogues/Details/{lensSetId}";
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, page);

        var (added, afterAdd) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("Label", "+2.00"), ("Sphere", P(2.00m)), ("Cylinder", P(0m)),
            ("Add", P(0m)), ("CoatingIds", Clear.ToString())));
        Assert.Equal(page, RedirectPath(added));
        Assert.Contains("+2.00", WebUtility.HtmlDecode(afterAdd));

        Guid lensId;
        using (var scope = factory.Services.CreateScope())
        {
            lensId = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().LensOptions
                .Single(l => l.PresetCatalogueId == lensSetId && l.Label == "+2.00").Id;
        }

        var (edited, _) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("LensOptionId", lensId.ToString()), ("Label", "+2.25"), ("Sphere", P(2.25m)),
            ("Cylinder", P(0m)), ("Add", P(0m)), ("CoatingIds", Clear.ToString())));
        Assert.Equal(page, RedirectPath(edited));

        var (removed, _) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RemoveLensOption",
            AdminPortalFactory.Form(token, ("lensOptionId", lensId.ToString())));
        Assert.Equal(page, RedirectPath(removed));

        var (renamed, afterRename) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/UpdateCatalogue", AdminPortalFactory.Form(token,
            ("Id", lensSetId.ToString()), ("Name", $"{lensSetName} renamed")));
        Assert.Equal(page, RedirectPath(renamed));
        Assert.Contains($"aria-label=\"Lenses in {lensSetName} renamed\"", afterRename);
    }

    [Fact]
    public async Task ARefusedLens_ReopensTheDialog_OnTheLensSetsPage()
    {
        var (lensSetId, lensSetName) = SeedLensSet("Refused");
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Catalogues/Details/{lensSetId}");

        // No label and no coatings: refused, and rendered straight back rather than redirected.
        var response = await client.PostAsync("/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("Label", ""), ("Sphere", P(2.00m)), ("Cylinder", P(0m)), ("Add", P(0m))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-open-on-load=\"true\"", html);
        Assert.Contains($"aria-label=\"Lenses in {lensSetName}\"", html);
    }

    [Fact]
    public async Task AssigningAndUnassigning_HappenOnTheLensSetsPage_OneOrganisationAtATime()
    {
        var (lensSetId, _) = SeedLensSet("Assigned");
        var page = $"/Catalogues/Details/{lensSetId}";
        var client = factory.CreateAdminClient();
        var before = await client.GetStringAsync(page);
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, page);

        var assignments = Section(before, "lensSetAssignments");
        Assert.Contains("Not assigned to any organisation yet.", assignments);
        Assert.Contains($"<option value=\"{KenyaRetailPoint}\"", assignments);
        Assert.DoesNotContain("multiple", assignments);

        var (assigned, afterAssign) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/AssignCatalogue", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()), ("OrgNodeId", KenyaRetailPoint.ToString())));
        Assert.Equal(page, RedirectPath(assigned));
        assignments = Section(afterAssign, "lensSetAssignments");
        Assert.Contains("/Catalogues/UnassignCatalogue", assignments);
        // An organisation it is already assigned to is not offered again.
        Assert.DoesNotContain($"<option value=\"{KenyaRetailPoint}\"", assignments);

        var (unassigned, afterUnassign) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/UnassignCatalogue", AdminPortalFactory.Form(token,
            ("catalogueId", lensSetId.ToString()), ("orgNodeId", KenyaRetailPoint.ToString())));
        Assert.Equal(page, RedirectPath(unassigned));
        Assert.Contains("Not assigned to any organisation yet.", afterUnassign);
    }

    // --- A retired lens set ------------------------------------------------------------------------

    [Fact]
    public async Task ARetiredLensSetsPage_IsReadOnly_ExceptForReactivate()
    {
        var (lensSetId, lensSetName) = SeedLensSet("Shelved", retired: true);
        factory.Seed(db => db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment
        {
            Id = Guid.NewGuid(), PresetCatalogueId = lensSetId, OrgNodeId = KenyaRetailPoint, CreatedAtUtc = DateTimeOffset.UtcNow,
        }));

        var html = await factory.CreateAdminClient().GetStringAsync($"/Catalogues/Details/{lensSetId}");

        // Its lenses and assignments are shown as they were…
        Assert.Contains($"aria-label=\"Lenses in {lensSetName}\"", html);
        Assert.Contains("Outreach Post", Text(Section(html, "lensSetAssignments")));
        // …and the only action is Reactivate.
        Assert.Contains("/Catalogues/ReactivateCatalogue", html);
        foreach (var write in new[] { "data-lens-add", "data-lens-edit", "SaveLens", "RemoveLensOption", "UpdateCatalogue", "RetireCatalogue", "AssignCatalogue", "UnassignCatalogue" })
        {
            Assert.DoesNotContain(write, html);
        }
    }

    [Fact]
    public async Task EveryWriteToARetiredLensSet_IsRefusedByTheServer_AndChangesNothing()
    {
        var (lensSetId, lensSetName) = SeedLensSet("Locked", retired: true);
        var otherOrg = OrganisationSeedConfiguration.KenyaId;
        Guid lensId = default;
        factory.Seed(db =>
        {
            lensId = db.LensOptions.Single(l => l.PresetCatalogueId == lensSetId).Id;
            db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment
            {
                Id = Guid.NewGuid(), PresetCatalogueId = lensSetId, OrgNodeId = KenyaRetailPoint, CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        });
        var page = $"/Catalogues/Details/{lensSetId}";
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, page);

        var writes = new (string Path, (string, string)[] Fields)[]
        {
            ("/Catalogues/UpdateCatalogue", [("Id", lensSetId.ToString()), ("Name", $"{lensSetName} renamed")]),
            ("/Catalogues/SaveLens", [("CatalogueId", lensSetId.ToString()), ("Label", "+2.00"), ("Sphere", P(2.00m)), ("Cylinder", P(0m)), ("Add", P(0m)), ("CoatingIds", Clear.ToString())]),
            ("/Catalogues/RemoveLensOption", [("lensOptionId", lensId.ToString())]),
            ("/Catalogues/AssignCatalogue", [("CatalogueId", lensSetId.ToString()), ("OrgNodeId", otherOrg.ToString())]),
            ("/Catalogues/UnassignCatalogue", [("catalogueId", lensSetId.ToString()), ("orgNodeId", KenyaRetailPoint.ToString())]),
            ("/Catalogues/RetireCatalogue", [("catalogueId", lensSetId.ToString())]),
        };
        foreach (var (path, fields) in writes)
        {
            var (_, html) = await AdminPortalFactory.PostAndFollowAsync(client, path, AdminPortalFactory.Form(token, fields), referer: page);
            Assert.True(html.Contains("This lens set is retired"), $"{path} was not refused on a retired lens set.");
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        Assert.Equal(lensSetName, (await db.PresetCatalogues.IgnoreQueryFilters().SingleAsync(c => c.Id == lensSetId)).Name);
        Assert.Equal([lensId], await db.LensOptions.Where(l => l.PresetCatalogueId == lensSetId).Select(l => l.Id).ToListAsync());
        Assert.Equal([KenyaRetailPoint], await db.PresetCatalogueAssignments.Where(a => a.PresetCatalogueId == lensSetId).Select(a => a.OrgNodeId).ToListAsync());
    }

    [Fact]
    public async Task ReactivatingALensSet_OpensItsPage_EditableAgain()
    {
        var (lensSetId, _) = SeedLensSet("Revived", retired: true);
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Catalogues/Details/{lensSetId}");

        var (reactivated, html) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/ReactivateCatalogue",
            AdminPortalFactory.Form(token, ("catalogueId", lensSetId.ToString())));

        Assert.Equal($"/Catalogues/Details/{lensSetId}", RedirectPath(reactivated));
        Assert.Contains("data-lens-add", html);
        Assert.NotNull(Row(await client.GetStringAsync("/Catalogues"), lensSetId));
    }
}
