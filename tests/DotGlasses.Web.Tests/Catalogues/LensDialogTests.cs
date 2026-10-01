using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>
/// The Add lens dialog (lens-power ticket 07, prototype variant A): an admin builds a lens set one
/// lens at a time with the shop's dropdowns, a typed label, the coatings the lens comes in and its
/// own pairings. The same dialog edits a lens. Saving checks everything at once and, when anything
/// is refused, the screen comes back with the dialog open, the admin's input still in it and every
/// problem next to its own field.
/// </summary>
public class LensDialogTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static readonly Guid Photochromic = ReferenceDataSeedConfiguration.CoatingPhotochromicId;
    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;
    private static readonly Guid BlueBlock = ReferenceDataSeedConfiguration.CoatingBlueBlockId;
    private static readonly Guid Polarized = new("b0000000-0000-0000-0000-000000000026");
    private static readonly Guid Sunglasses = new("b0000000-0000-0000-0000-000000000027");
    private static readonly Guid Bifocal = ReferenceDataSeedConfiguration.LensTypeBifocalId;
    private static readonly Guid Progressive = new("b0000000-0000-0000-0000-000000000060");
    private static readonly Guid OtherLensType = new("b0000000-0000-0000-0000-000000000061");

    /// <summary>A form value the way the dialog's own options carry it — the request's culture,
    /// which is also the culture the model binder parses it back with.</summary>
    private static string P(decimal value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    private static string VisibleText(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    /// <summary>The lens table for this lens set, as an admin reads it. The screen shows one lens
    /// set at a time, so the table is only there when this is the one selected.</summary>
    private static string TableFor(string html, string lensSetName)
    {
        var match = Regex.Match(html, $"<table[^>]*aria-label=\"Lenses in {Regex.Escape(lensSetName)}\"[^>]*>(.*?)</table>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No lens table for {lensSetName}.");
        return VisibleText(match.Groups[1].Value);
    }

    /// <summary>The dialog, which the screen renders once, after the selected lens set's card.</summary>
    private static string Dialog(string html)
    {
        var match = Regex.Match(html, "<div class=\"modal fade\" id=\"lensDialog\".*?<!-- /lensDialog -->", RegexOptions.Singleline);
        Assert.True(match.Success, "No lens dialog on the page.");
        return match.Value;
    }

    /// <summary>The message shown next to one of the dialog's fields, keyed by the request's own
    /// property name.</summary>
    private static string ErrorFor(string html, string key)
    {
        var match = Regex.Match(Dialog(html), $"data-error-for=\"{Regex.Escape(key)}\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No error slot for {key}.");
        return VisibleText(match.Groups[1].Value);
    }

    private (Guid Id, string Name) SeedLensSet(Action<Guid, Infrastructure.Persistence.DotGlassesDbContext>? lenses = null)
    {
        var id = Guid.NewGuid();
        var name = $"Dialog Readers {id:N}";
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = id, Name = name, OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            lenses?.Invoke(id, db);
        });
        return (id, name);
    }

    private static Guid AddLens(Infrastructure.Persistence.DotGlassesDbContext db, Guid lensSetId, string label, decimal sphere,
        decimal? add = null, Guid? lensType = null, params Guid[] coatings)
    {
        var lensId = Guid.NewGuid();
        db.LensOptions.Add(new LensOption { Id = lensId, PresetCatalogueId = lensSetId, Label = label, Sphere = sphere, Add = add, LensTypeRefId = lensType });
        foreach (var coating in coatings.DefaultIfEmpty(Clear))
        {
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = coating });
        }

        return lensId;
    }

    private static async Task<HttpResponseMessage> SaveAsync(HttpClient client, params (string Key, string Value)[] fields)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        return await client.PostAsync("/Catalogues/SaveLens", AdminPortalFactory.Form(token, fields));
    }

    [Fact]
    public async Task TheDialogsDropdowns_AreTheRulesAllowedValues_InTheShopsOrder()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues");
        var dialog = Dialog(html);

        IReadOnlyList<string> Options(string name)
        {
            var select = Regex.Match(dialog, $"<select[^>]*name=\"{name}\".*?</select>", RegexOptions.Singleline);
            Assert.True(select.Success, $"No {name} dropdown.");
            return Regex.Matches(select.Value, "<option value=\"([^\"]+)\"[^>]*>(.*?)</option>", RegexOptions.Singleline)
                .Select(m => WebUtility.HtmlDecode(m.Groups[2].Value).Trim())
                .ToList();
        }

        Assert.Equal(LensPowerValues.Sphere.Select(LensPowerValues.FormatPower), Options("Sphere"));
        Assert.Equal(LensPowerValues.Cylinder.Select(LensPowerValues.FormatPower), Options("Cylinder"));
        Assert.Equal(LensPowerValues.Axis.Select(a => a.ToString("0", CultureInfo.InvariantCulture)), Options("Axis"));
        Assert.Equal(LensPowerValues.Add.Select(LensPowerValues.FormatPower), Options("Add"));
    }

    [Fact]
    public async Task TheDialogOffersTheLensTypes_TheCoatings_AndListsTheExclusionsAsANote()
    {
        factory.Seed(db =>
        {
            var (a, b) = CoatingExclusion.Canonicalize(Clear, Polarized);
            if (!db.CoatingExclusions.Any(e => e.CoatingRefIdA == a && e.CoatingRefIdB == b))
            {
                db.CoatingExclusions.Add(new CoatingExclusion { Id = Guid.NewGuid(), CoatingRefIdA = a, CoatingRefIdB = b, CreatedAtUtc = DateTimeOffset.UtcNow });
            }
        });

        var dialog = VisibleText(Dialog(await factory.CreateAdminClient().GetStringAsync("/Catalogues")));

        Assert.Contains("Spherical power", dialog);
        Assert.Contains("Cylindrical power", dialog);
        Assert.Contains("Axis", dialog);
        Assert.Contains("Add near vision power", dialog);
        Assert.Contains("Bifocal", dialog);
        Assert.Contains("Progressive", dialog);
        Assert.Contains("Photochromic", dialog);
        Assert.Contains("Pairings for this lens", dialog);
        Assert.Matches("Exclusions still apply.*(Clear and Polarized|Polarized and Clear)", dialog);
    }

    [Fact]
    public async Task AddingALens_ListsItInTheSetsTable_WithItsLensType_Coatings_AndPairing()
    {
        var (lensSetId, lensSetName) = SeedLensSet();
        var client = factory.CreateAdminClient();

        var response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("Label", "  Bifocal +2.00 "),
            ("Sphere", P(0m)),
            ("Cylinder", P(0m)),
            ("Add", P(2.00m)),
            ("LensTypeRefId", Bifocal.ToString()),
            ("CoatingIds", Photochromic.ToString()),
            ("CoatingIds", BlueBlock.ToString()),
            ("Pairings[0].TriggerCoatingRefId", BlueBlock.ToString()),
            ("Pairings[0].PairedCoatingRefId", Photochromic.ToString()));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("Bifocal +2.00 SPH 0.00 · ADD +2.00 Bifocal Photochromic Blue block Blue block → Photochromic Edit Remove", TableFor(html, lensSetName));
    }

    [Fact]
    public async Task ALensWithACylinder_KeepsItsAxis()
    {
        var (lensSetId, lensSetName) = SeedLensSet();
        var client = factory.CreateAdminClient();

        var response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("Label", "-2.00 astig"),
            ("Sphere", P(-2.00m)),
            ("Cylinder", P(-0.75m)),
            ("Axis", "90"),
            ("Add", P(0m)),
            ("CoatingIds", Clear.ToString()));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("-2.00 astig SPH -2.00 · CYL -0.75 × 90 Single vision Clear —", TableFor(await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}"), lensSetName));
    }

    [Fact]
    public async Task TheTable_ListsLensesInTheRulesOrder_WhateverOrderTheyWereAddedIn()
    {
        var (lensSetId, lensSetName) = SeedLensSet();
        var client = factory.CreateAdminClient();

        foreach (var (label, sphere, add, lensType) in new (string, decimal, decimal, Guid?)[]
                 {
                     ("Progressive +1.00", 0m, 1.00m, Progressive),
                     ("Bifocal +2.00", 0m, 2.00m, Bifocal),
                     ("+1.50", 1.50m, 0m, null),
                     ("Bifocal +1.00", 0m, 1.00m, Bifocal),
                     ("-0.50", -0.50m, 0m, null),
                 })
        {
            var fields = new List<(string, string)>
            {
                ("CatalogueId", lensSetId.ToString()), ("Label", label), ("Sphere", P(sphere)), ("Cylinder", P(0m)),
                ("Add", P(add)), ("CoatingIds", Clear.ToString()),
            };
            if (lensType is { } type)
            {
                fields.Add(("LensTypeRefId", type.ToString()));
            }

            Assert.Equal(HttpStatusCode.Found, (await SaveAsync(client, [.. fields])).StatusCode);
        }

        var labels = Regex.Matches(TableFor(await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}"), lensSetName), @"(-0\.50|\+1\.50|Bifocal \+1\.00|Bifocal \+2\.00|Progressive \+1\.00) SPH")
            .Select(m => m.Groups[1].Value);
        Assert.Equal(["-0.50", "+1.50", "Bifocal +1.00", "Bifocal +2.00", "Progressive +1.00"], labels);
    }

    [Fact]
    public async Task EditingALens_ChangesItInPlace_AndItsOwnLabelAndPowerDontCountAsTaken()
    {
        Guid lensId = default;
        var (lensSetId, lensSetName) = SeedLensSet((setId, db) =>
        {
            lensId = AddLens(db, setId, "+2.50", 2.50m, coatings: [Clear, BlueBlock]);
            db.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing { Id = Guid.NewGuid(), LensOptionId = lensId, TriggerCoatingRefId = BlueBlock, PairedCoatingRefId = Clear });
            AddLens(db, setId, "+3.00", 3.00m);
        });
        var client = factory.CreateAdminClient();

        // The Edit button carries the lens as it is, as the dialog's own option values, for the
        // dialog to open with.
        var before = await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}");
        var editData = Regex.Match(before, $"data-lens=\"([^\"]*{lensId}[^\"]*)\"");
        Assert.True(editData.Success, "No Edit button for the lens.");
        using (var lens = JsonDocument.Parse(WebUtility.HtmlDecode(editData.Groups[1].Value)))
        {
            Assert.Equal("+2.50", lens.RootElement.GetProperty("label").GetString());
            Assert.Equal(P(2.50m), lens.RootElement.GetProperty("sphere").GetString());
            Assert.Equal(P(0m), lens.RootElement.GetProperty("cylinder").GetString());
            Assert.Equal("", lens.RootElement.GetProperty("axis").GetString());
            Assert.Equal(P(0m), lens.RootElement.GetProperty("add").GetString());
            Assert.Equal(2, lens.RootElement.GetProperty("coatingIds").GetArrayLength());
            var pairing = Assert.Single(lens.RootElement.GetProperty("pairings").EnumerateArray());
            Assert.Equal(BlueBlock, pairing.GetProperty("triggerCoatingRefId").GetGuid());
            Assert.Equal(Clear, pairing.GetProperty("pairedCoatingRefId").GetGuid());
        }

        // Same label and power as it already has, the pairing dropped, a coating added.
        var response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("LensOptionId", lensId.ToString()),
            ("Label", "+2.50"),
            ("Sphere", P(2.50m)),
            ("Cylinder", P(0m)),
            ("Add", P(0m)),
            ("CoatingIds", Clear.ToString()),
            ("CoatingIds", Photochromic.ToString()));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        // Then relabelled and re-powered.
        response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("LensOptionId", lensId.ToString()),
            ("Label", "Readers +2.75"),
            ("Sphere", P(2.75m)),
            ("Cylinder", P(0m)),
            ("Add", P(0m)),
            ("CoatingIds", Photochromic.ToString()));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var table = TableFor(await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}"), lensSetName);
        Assert.Contains("Readers +2.75 SPH +2.75 Single vision Photochromic —", table);
        Assert.Contains("+3.00 SPH +3.00", table);
        Assert.DoesNotContain("+2.50", table);
        Assert.Equal(2, Regex.Matches(table, " SPH ").Count);
    }

    [Fact]
    public async Task RemovingALens_TakesItOutOfTheSet()
    {
        Guid lensId = default;
        var (_, lensSetName) = SeedLensSet((setId, db) =>
        {
            lensId = AddLens(db, setId, "-1.75", -1.75m);
            AddLens(db, setId, "+0.75", 0.75m);
        });
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        var (_, html) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RemoveLensOption",
            AdminPortalFactory.Form(token, ("lensOptionId", lensId.ToString())));

        var table = TableFor(html, lensSetName);
        Assert.DoesNotContain("-1.75", table);
        Assert.Contains("+0.75 SPH +0.75 Single vision Clear — Edit Remove", table);
    }

    [Fact]
    public async Task EveryProblemIsReturnedAtOnce_NextToItsField_WithTheDialogReopenedOnTheAdminsInput()
    {
        var (lensSetId, lensSetName) = SeedLensSet((setId, db) => AddLens(db, setId, "Readers", 1.00m));
        var client = factory.CreateAdminClient();

        var response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("Label", "READERS"),                   // taken, ignoring case
            ("Sphere", P(10.25m)),                  // outside the allowed values
            ("Cylinder", P(-1.00m)),                // … with no axis
            ("Add", P(2.00m)),                      // an add, with no lens type
            ("Pairings[0].TriggerCoatingRefId", BlueBlock.ToString()),  // no coatings ticked,
            ("Pairings[0].PairedCoatingRefId", Photochromic.ToString())); // so neither is ticked

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("already has a lens labelled \"READERS\"", ErrorFor(html, "Label"));
        Assert.Contains("Spherical power must be between -10 and 10", ErrorFor(html, "Sphere"));
        Assert.Contains("Axis is required when Cylindrical power isn't 0.00", ErrorFor(html, "Axis"));
        Assert.Contains("Lens type is required", ErrorFor(html, "LensTypeRefId"));
        Assert.Contains("Tick at least one coating", ErrorFor(html, "CoatingIds"));
        Assert.Contains("must be ticked", ErrorFor(html, "Pairings[0]"));

        // Reopened on what was typed, not on a blank form — and saved nothing.
        var dialog = Dialog(html);
        Assert.Contains("data-open-on-load=\"true\"", dialog);
        Assert.Contains($"name=\"CatalogueId\" value=\"{lensSetId}\"", dialog);
        Assert.Contains("value=\"READERS\"", dialog);
        Assert.Matches($"<option value=\"{Regex.Escape(P(-1.00m))}\" selected", dialog);
        Assert.Matches($"<option value=\"{Regex.Escape(BlueBlock.ToString())}\" selected", dialog);
        Assert.Contains($"Add lens to {lensSetName}", VisibleText(dialog));
        Assert.Single(Regex.Matches(TableFor(html, lensSetName), " SPH "));

        // The dialog carries the problems; the page doesn't repeat them above the lens sets.
        Assert.DoesNotContain("validation-summary-errors", html[..html.IndexOf("id=\"lensDialog\"", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task ABlankLabel_ADuplicatePowerAndLensType_AndEveryBadPairing_AreAllRefusedTogether()
    {
        factory.Seed(db =>
        {
            var (a, b) = CoatingExclusion.Canonicalize(BlueBlock, Sunglasses);
            if (!db.CoatingExclusions.Any(e => e.CoatingRefIdA == a && e.CoatingRefIdB == b))
            {
                db.CoatingExclusions.Add(new CoatingExclusion { Id = Guid.NewGuid(), CoatingRefIdA = a, CoatingRefIdB = b, CreatedAtUtc = DateTimeOffset.UtcNow });
            }
        });
        var (lensSetId, _) = SeedLensSet((setId, db) => AddLens(db, setId, "Bifocal +2.00", 0m, 2.00m, Bifocal));
        var client = factory.CreateAdminClient();

        var response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("Label", "   "),
            ("Sphere", P(0m)),
            ("Cylinder", P(0m)),
            ("Add", P(2.00m)),
            ("LensTypeRefId", Bifocal.ToString()),   // the same power and lens type as the set's bifocal
            ("CoatingIds", Clear.ToString()),
            ("CoatingIds", BlueBlock.ToString()),
            ("CoatingIds", Sunglasses.ToString()),
            ("Pairings[0].TriggerCoatingRefId", Clear.ToString()),      // self-paired
            ("Pairings[0].PairedCoatingRefId", Clear.ToString()),
            ("Pairings[1].TriggerCoatingRefId", BlueBlock.ToString()),  // fine…
            ("Pairings[1].PairedCoatingRefId", Clear.ToString()),
            ("Pairings[2].TriggerCoatingRefId", BlueBlock.ToString()),  // … but listed twice
            ("Pairings[2].PairedCoatingRefId", Clear.ToString()),
            ("Pairings[3].TriggerCoatingRefId", BlueBlock.ToString()),  // forbidden by an exclusion
            ("Pairings[3].PairedCoatingRefId", Sunglasses.ToString()),
            ("Pairings[4].TriggerCoatingRefId", BlueBlock.ToString()),  // half a pairing
            ("Pairings[4].PairedCoatingRefId", ""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Enter the label", ErrorFor(html, "Label"));
        Assert.Contains("already has a lens with this power and lens type", ErrorFor(html, "Sphere"));
        Assert.Contains("can't pair with itself", ErrorFor(html, "Pairings[0]"));
        Assert.Equal("", ErrorFor(html, "Pairings[1]"));
        Assert.Contains("listed twice", ErrorFor(html, "Pairings[2]"));
        Assert.Contains("Blue block and Sunglasses exclude each other", ErrorFor(html, "Pairings[3]"));
        Assert.Contains("Choose both coatings", ErrorFor(html, "Pairings[4]"));
        Assert.Equal("", ErrorFor(html, "CoatingIds"));
        Assert.Equal("", ErrorFor(html, "LensTypeRefId"));
    }

    [Fact]
    public async Task TheSamePower_IsAllowedAgainWithADifferentLensType()
    {
        var (lensSetId, lensSetName) = SeedLensSet((setId, db) => AddLens(db, setId, "Bifocal +2.00", 0m, 2.00m, Bifocal));
        var client = factory.CreateAdminClient();

        var response = await SaveAsync(client,
            ("CatalogueId", lensSetId.ToString()),
            ("Label", "Progressive +2.00"),
            ("Sphere", P(0m)),
            ("Cylinder", P(0m)),
            ("Add", P(2.00m)),
            ("LensTypeRefId", Progressive.ToString()),
            ("CoatingIds", Clear.ToString()));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("Progressive +2.00 SPH 0.00 · ADD +2.00 Progressive", TableFor(await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}"), lensSetName));
    }

    [Fact]
    public async Task AnOtherLensType_NeedsItsText_AndShowsItOnceGiven()
    {
        var (lensSetId, lensSetName) = SeedLensSet();
        var client = factory.CreateAdminClient();
        (string, string)[] Lens(string otherText) =>
        [
            ("CatalogueId", lensSetId.ToString()), ("Label", "Trifocal +1.50"), ("Sphere", P(0m)), ("Cylinder", P(0m)),
            ("Add", P(1.50m)), ("LensTypeRefId", OtherLensType.ToString()), ("LensTypeOtherText", otherText),
            ("CoatingIds", Clear.ToString()),
        ];

        var refused = await SaveAsync(client, Lens(" "));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Other lens type is required", ErrorFor(await refused.Content.ReadAsStringAsync(), "LensTypeOtherText"));

        var saved = await SaveAsync(client, Lens("Trifocal"));
        Assert.Equal(HttpStatusCode.Found, saved.StatusCode);
        Assert.Contains("Trifocal +1.50 SPH 0.00 · ADD +1.50 Trifocal Clear", TableFor(await client.GetStringAsync($"/Catalogues?catalogueId={lensSetId}"), lensSetName));
    }

    [Fact]
    public async Task ALensSetOwnedAboveTheCaller_CantHaveLensesAddedByAHandBuiltPost()
    {
        var (lensSetId, _) = SeedLensSet();  // owned by DGI
        var countryAdmin = factory.CreateAdminClient(OrganisationLevel.Country);

        var response = await SaveAsync(countryAdmin,
            ("CatalogueId", lensSetId.ToString()), ("Label", "+1.00"), ("Sphere", P(1.00m)), ("Cylinder", P(0m)),
            ("Add", P(0m)), ("CoatingIds", Clear.ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditingALensRemovedSinceThePageLoaded_SaysSo_RatherThanRefusingAccess()
    {
        var (lensSetId, _) = SeedLensSet();
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        var (redirect, html) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/SaveLens",
            AdminPortalFactory.Form(token,
                ("CatalogueId", lensSetId.ToString()), ("LensOptionId", Guid.NewGuid().ToString()), ("Label", "+1.00"),
                ("Sphere", P(1.00m)), ("Cylinder", P(0m)), ("Add", P(0m)), ("CoatingIds", Clear.ToString())),
            referer: "/Catalogues");

        Assert.Equal("/Catalogues", redirect.Headers.Location?.ToString());
        Assert.Contains("This lens is no longer in the lens set", html);
    }

    [Fact]
    public async Task ALensCantBeEditedThroughAnotherLensSet()
    {
        Guid lensId = default;
        SeedLensSet((setId, db) => lensId = AddLens(db, setId, "+1.00", 1.00m));
        var (otherLensSetId, _) = SeedLensSet();
        var client = factory.CreateAdminClient();

        var response = await SaveAsync(client,
            ("CatalogueId", otherLensSetId.ToString()), ("LensOptionId", lensId.ToString()), ("Label", "+1.25"),
            ("Sphere", P(1.25m)), ("Cylinder", P(0m)), ("Add", P(0m)), ("CoatingIds", Clear.ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
