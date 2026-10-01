using System.Text.Json;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.LeadConversion;

/// <summary>
/// The Admin Portal's Lead conversion lens section (ADR-0007): the same choices, in the same order,
/// as the Field App's — "Same lens for both eyes", the right eye limited to the left's lens type, a
/// power line under each lens, the coatings both lenses come in with the pairings locked, and a
/// Lead's lens found by matching (LensSetLenses.Match) with a note when it is no longer in the set.
/// What the browser script does on top (show/hide, refetching the offered coatings) is progressive:
/// these tests pin what the server renders and what it refuses without it.
/// </summary>
public class LeadConversionLensSectionTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;
    private static readonly Guid BlueBlock = ReferenceDataSeedConfiguration.CoatingBlueBlockId;
    private static readonly Guid Bifocal = ReferenceDataSeedConfiguration.LensTypeBifocalId;

    /// <summary>A lens set reaching the Kenya retail point: +2.50 (Clear and Blue block, Blue block
    /// bringing Clear), +3.00 (Clear only) and a +2.00 Bifocal with a +1.00 add (Clear).</summary>
    private sealed record TestSet(string Name, Guid Id, Guid Plus250, Guid Plus300, Guid BifocalLens);

    private TestSet SeedLensSet()
    {
        var setId = Guid.NewGuid();
        var (plus250, plus300, bifocal) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = setId, Name = $"Kenya Readers {setId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = setId, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });

            db.LensOptions.Add(new LensOption { Id = plus250, PresetCatalogueId = setId, Label = "Reader 2.5", Sphere = 2.50m });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = plus250, CoatingRefId = Clear });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = plus250, CoatingRefId = BlueBlock });
            db.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing { Id = Guid.NewGuid(), LensOptionId = plus250, TriggerCoatingRefId = BlueBlock, PairedCoatingRefId = Clear });

            db.LensOptions.Add(new LensOption { Id = plus300, PresetCatalogueId = setId, Label = "Reader 3.0", Sphere = 3.00m });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = plus300, CoatingRefId = Clear });

            db.LensOptions.Add(new LensOption { Id = bifocal, PresetCatalogueId = setId, Label = "Bifocal 2.0", Sphere = 2.00m, Add = 1.00m, LensTypeRefId = Bifocal });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = bifocal, CoatingRefId = Clear });
        });
        return new TestSet($"Kenya Readers {setId:N}", setId, plus250, plus300, bifocal);
    }

    private Guid SeedLead(Action<Lead>? configure = null)
    {
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.Customers.Add(new Customer { Id = customerId, FullName = "Wanjiru Kamau", PhoneNumber = "+254711000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            var lead = new Lead { Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true };
            configure?.Invoke(lead);
            db.Leads.Add(lead);
        });
        return leadId;
    }

    private T Query<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }

    private Guid AFrameColour() =>
        Query(db => db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.FrameColour && x.IsActive && !x.IsOtherOption).Id);

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid leadId, params (string Key, string Value)[] fields)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        var all = new List<(string Key, string Value)> { ("Form.ConsentGiven", "true"), ("Form.FrameColourRefId", AFrameColour().ToString()) };
        all.AddRange(fields);
        return await client.PostAsync($"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token, all.ToArray()));
    }

    /// <summary>The conversion screen, entity-decoded so a "+" in a power reads as one.</summary>
    private async Task<string> PageAsync(Guid leadId) =>
        System.Net.WebUtility.HtmlDecode(await factory.CreateAdminClient().GetStringAsync($"/Leads/Convert/{leadId}"));

    // ---- "Same lens for both eyes" ----

    [Fact]
    public async Task TheLensSetChoiceStartsWithSameLensForBothEyesTicked()
    {
        var leadId = SeedLead();

        var html = await PageAsync(leadId);

        Assert.Matches(new Regex("<input[^>]*type=\"checkbox\"[^>]*name=\"Form.SameLensForBothEyes\"[^>]*checked=\"checked\"|<input[^>]*checked=\"checked\"[^>]*name=\"Form.SameLensForBothEyes\""), html);
    }

    [Fact]
    public async Task ConvertingWithSameLensForBothEyes_RecordsThatLensOnBothEyes_FromOneChoice()
    {
        var set = SeedLensSet();
        var leadId = SeedLead();

        var response = await PostAsync(factory.CreateAdminClient(), leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "true"),
            ("Form.LensLeftId", set.Plus300.ToString()),
            // What a browser without the script would still post for the hidden right eye — the
            // ticked box says one lens is both, so this must not win.
            ("Form.LensRightId", set.Plus250.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", Clear.ToString()));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal((set.Id, 3.00m, 3.00m), (sale.PresetCatalogueId!.Value, sale.SphereLeft!.Value, sale.SphereRight!.Value));
        Assert.Null(sale.LensTypeRefId);
    }

    [Fact]
    public async Task SameLensForBothEyesUnticked_RecordsEachEyesOwnLens()
    {
        var set = SeedLensSet();
        var leadId = SeedLead();

        await PostAsync(factory.CreateAdminClient(), leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "false"),
            ("Form.LensLeftId", set.Plus250.ToString()),
            ("Form.LensRightId", set.Plus300.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", Clear.ToString()));

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal((2.50m, 3.00m), (sale.SphereLeft!.Value, sale.SphereRight!.Value));
    }

    [Fact]
    public async Task ARightLensOfAnotherLensTypeIsRefusedAgainstTheRightEyesLens()
    {
        var set = SeedLensSet();
        var leadId = SeedLead();

        var response = await PostAsync(factory.CreateAdminClient(), leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "false"),
            ("Form.LensLeftId", set.Plus250.ToString()),
            ("Form.LensRightId", set.BifocalLens.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", Clear.ToString()));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Query(db => db.Sales.IgnoreQueryFilters().Where(s => s.SourceLeadId == leadId).ToList()));
        Assert.Contains("Choose a right-eye lens of the same lens type as the left eye's", ErrorFor(html, "Form.SphereRight"));
    }

    [Fact]
    public async Task TheRightEyesChoicesAreLimitedToTheLeftEyesLensType()
    {
        // Seeded from a Lead whose two eyes are different lenses: the box starts unticked, the left
        // is a Bifocal, and the right dropdown offers only Bifocals (a pair has one lens type).
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 2.00m;
            l.AddLeft = 1.00m;
            l.LensTypeRefId = Bifocal;
            l.SphereRight = 9.00m; // matches nothing → this eye is unmatched
            l.AddRight = 1.00m;
            l.PresetPupilDistanceBucket = 2;
        });

        var html = await PageAsync(leadId);

        Assert.Equal(set.BifocalLens.ToString(), SelectedValue(html, "Form.LensLeftId"));
        var rightOptions = OptionValues(html, "Form.LensRightId");
        Assert.Equal([string.Empty, set.BifocalLens.ToString()], rightOptions);
    }

    // ---- The lens choices and their order ----

    [Fact]
    public async Task TheLensDropdownListsTheChosenSetsLensesByLabelInTheRulesOrder_WithAPowerLine()
    {
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 9.00m; // no lens holds it → the set is chosen, the lens is left to the admin
            l.SphereRight = 9.00m;
            l.PresetPupilDistanceBucket = 2;
        });

        var html = await PageAsync(leadId);

        // Single vision by sphere, then the Bifocal — labels, not "<set> — <label>".
        Assert.Equal(
            [string.Empty, set.Plus250.ToString(), set.Plus300.ToString(), set.BifocalLens.ToString()],
            OptionValues(html, "Form.LensLeftId"));
        Assert.Contains(">Reader 2.5<", html);
        Assert.DoesNotContain($"{set.Name} — Reader", html);

        // The power behind each label is in the page for the script's power line (and shown for a
        // lens that is already chosen — see the next test).
        var data = Regex.Match(html, "<script type=\"application/json\" data-lens-sets>(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        using var doc = JsonDocument.Parse(data);
        var lenses = doc.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == set.Id).GetProperty("lenses").EnumerateArray()
            .ToDictionary(l => l.GetProperty("label").GetString()!, l => l.GetProperty("power").GetString());
        Assert.Equal("SPH +2.50", lenses["Reader 2.5"]);
        Assert.Equal("SPH +2.00 · ADD +1.00", lenses["Bifocal 2.0"]);
    }

    [Fact]
    public async Task AChosenLensShowsItsPowerUnderTheDropdown()
    {
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 2.00m;
            l.AddLeft = 1.00m;
            l.LensTypeRefId = Bifocal;
            l.SphereRight = 9.00m; // unmatched, so the lens choice is on the page rather than a summary
            l.AddRight = 1.00m;
            l.PresetPupilDistanceBucket = 2;
        });

        var html = await PageAsync(leadId);

        Assert.Matches(new Regex("data-power-line=\"left\"[^>]*>SPH \\+2\\.00 · ADD \\+1\\.00<"), html);
    }

    // ---- Seeding by matching, and the note ----

    [Fact]
    public async Task ALeadWhoseLensIsNoLongerInTheSet_LeavesThatEyeEmptyWithTheNote_AndSameLensUnticked()
    {
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 2.50m;
            l.SphereRight = 3.50m; // gone from the set since the Lead was captured
            l.PresetPupilDistanceBucket = 2;
        });

        var html = await PageAsync(leadId);

        Assert.Contains($"The SPH +3.50 on this Lead is no longer in {set.Name}. Choose a lens.", html);
        Assert.DoesNotContain("SPH +2.50 on this Lead is no longer", html);
        Assert.Equal(set.Plus250.ToString(), SelectedValue(html, "Form.LensLeftId"));
        Assert.Null(SelectedValue(html, "Form.LensRightId"));
        Assert.DoesNotMatch(new Regex("name=\"Form.SameLensForBothEyes\"[^>]*checked=\"checked\"|checked=\"checked\"[^>]*name=\"Form.SameLensForBothEyes\""), html);
    }

    [Fact]
    public async Task ALeadWithTheSameLensOnBothEyes_NoLongerInTheSet_StartsSameLensTicked_WithOneEmptyChoiceAndOneNote()
    {
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 3.50m;
            l.SphereRight = 3.50m;
            l.PresetPupilDistanceBucket = 2;
        });

        var html = await PageAsync(leadId);

        // One note, under the one "Lens" dropdown: the left note is showing and the right eye's
        // whole field is out of sight.
        var leftNote = Regex.Match(html, "<p data-lens-note=\"left\"([^>]*)>([^<]*)</p>");
        Assert.DoesNotContain("hidden=", leftNote.Groups[1].Value);
        Assert.Equal($"The SPH +3.50 on this Lead is no longer in {set.Name}. Choose a lens.", leftNote.Groups[2].Value);
        Assert.Matches(new Regex("data-right-lens-field[^>]*hidden=\"hidden\""), html);
        Assert.Null(SelectedValue(html, "Form.LensLeftId"));
        Assert.Matches(new Regex("name=\"Form.SameLensForBothEyes\"[^>]*checked=\"checked\"|checked=\"checked\"[^>]*name=\"Form.SameLensForBothEyes\""), html);
    }

    [Fact]
    public async Task TheNoteStandsUntilALensIsChosen_ThenTheAdminsChoiceConvertsTheLead()
    {
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 3.50m;
            l.SphereRight = 3.50m;
            l.PresetPupilDistanceBucket = 2;
        });
        var client = factory.CreateAdminClient();

        // Submitting without choosing is refused against the lens, and the note is still there.
        var refused = await PostAsync(client, leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "true"),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", Clear.ToString()));
        var html = await refused.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Choose a lens for the left eye.", ErrorFor(html, "Form.SphereLeft"));
        Assert.Contains("no longer in " + set.Name, html);

        var converted = await PostAsync(client, leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "true"),
            ("Form.LensLeftId", set.Plus250.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", Clear.ToString()));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, converted.StatusCode);
        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal((2.50m, 2.50m), (sale.SphereLeft!.Value, sale.SphereRight!.Value));
    }

    // ---- A server rejection lands on the right control ----

    [Fact]
    public async Task ACoatingTheChosenLensesDontBothComeIn_IsRefusedAgainstTheCoatings()
    {
        var set = SeedLensSet();
        var leadId = SeedLead();

        var response = await PostAsync(factory.CreateAdminClient(), leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "false"),
            ("Form.LensLeftId", set.Plus250.ToString()),
            ("Form.LensRightId", set.Plus300.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", BlueBlock.ToString()));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("isn't made on these lenses", ErrorFor(html, "Form.CoatingRefIds"));
    }

    [Fact]
    public async Task ATriggerCoatingWithoutItsPairedCoating_IsRefusedAgainstTheCoatings()
    {
        var set = SeedLensSet();
        var leadId = SeedLead();

        var response = await PostAsync(factory.CreateAdminClient(), leadId,
            ("Form.LensRange", set.Id.ToString()),
            ("Form.SameLensForBothEyes", "true"),
            ("Form.LensLeftId", set.Plus250.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.CoatingRefIds", BlueBlock.ToString()));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Clear comes with Blue block on these lenses", ErrorFor(html, "Form.CoatingRefIds"));
    }

    [Fact]
    public async Task ACustomPrescriptionsRejectionLandsOnItsOwnControl()
    {
        var leadId = SeedLead();

        var response = await PostAsync(factory.CreateAdminClient(), leadId,
            ("Form.LensRange", "custom"),
            ("Form.SphereLeft", "2.5"),
            ("Form.SphereRight", "2.5"),
            ("Form.AddLeft", "1.5"),
            ("Form.PupilDistanceMm", "63"),
            ("Form.CoatingRefIds", Clear.ToString()));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(ErrorFor(html, "Form.LensTypeRefId"));
    }

    // ---- The coatings both lenses come in ----

    [Fact]
    public async Task ALeadWhoseLensCarriesOver_OffersOnlyTheCoatingsItsLensesComeIn_AndLocksThePairing()
    {
        var set = SeedLensSet();
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = set.Id;
            l.SphereLeft = 2.50m;
            l.SphereRight = 2.50m;
            l.PresetPupilDistanceBucket = 2;
            l.CoatingPreferenceRefId = BlueBlock;
        });

        var html = await PageAsync(leadId);

        Assert.Equal(new[] { BlueBlock, Clear }.Select(g => g.ToString()).Order(), OfferedCoatings(html).Order());
        // The Lead's preference seeds the set (SaleAssembly.Seed); the pairing it brings is ticked and locked.
        Assert.Contains("Comes with Blue block on this lens", html);
        Assert.Contains(Clear.ToString(), CheckedCoatings(html));
        Assert.Contains(BlueBlock.ToString(), CheckedCoatings(html));
    }

    [Fact]
    public async Task TheOfferedCoatingsForAPairOfLenses_AreServedFromTheSameRulesTheServerChecksWith()
    {
        var set = SeedLensSet();
        var leadId = SeedLead();
        var client = factory.CreateAdminClient();

        var both = await client.GetStringAsync($"/Leads/Convert/{leadId}/coatings?left={set.Plus250}&right={set.Plus250}");
        using (var doc = JsonDocument.Parse(both))
        {
            var offered = doc.RootElement.GetProperty("offered").EnumerateArray().Select(e => e.GetGuid()).Order().ToList();
            Assert.Equal(new[] { Clear, BlueBlock }.Order().ToList(), offered);
            // What ticking each trigger does, worked out by the server (PairedCoatings): Blue block
            // brings Clear and locks it.
            var effect = Assert.Single(doc.RootElement.GetProperty("effects").EnumerateObject());
            Assert.Equal(BlueBlock, Guid.Parse(effect.Name));
            Assert.Equal([Clear], effect.Value.GetProperty("brings").EnumerateArray().Select(e => e.GetGuid()));
            Assert.Equal([Clear], effect.Value.GetProperty("locks").EnumerateArray().Select(e => e.GetGuid()));
        }

        var mixed = await client.GetStringAsync($"/Leads/Convert/{leadId}/coatings?left={set.Plus250}&right={set.Plus300}");
        using (var doc = JsonDocument.Parse(mixed))
        {
            Assert.Equal([Clear], doc.RootElement.GetProperty("offered").EnumerateArray().Select(e => e.GetGuid()).ToList());
        }

        var unknown = await client.GetStringAsync($"/Leads/Convert/{leadId}/coatings?left={Guid.NewGuid()}");
        using var none = JsonDocument.Parse(unknown);
        Assert.Equal(JsonValueKind.Null, none.RootElement.GetProperty("offered").ValueKind);
    }

    [Fact]
    public async Task APairingThatRunsBothWays_TicksBothCoatings_ButLocksNeither()
    {
        // Blue block → Clear and Clear → Blue block on one lens: each would hold the other ticked for
        // ever, so — as on the Field App (PairedCoatings) — neither is locked.
        var setId = Guid.NewGuid();
        var lensId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = setId, Name = $"Both Ways {setId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = setId, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });
            db.LensOptions.Add(new LensOption { Id = lensId, PresetCatalogueId = setId, Label = "Reader 2.5", Sphere = 2.50m });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = Clear });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = BlueBlock });
            db.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing { Id = Guid.NewGuid(), LensOptionId = lensId, TriggerCoatingRefId = BlueBlock, PairedCoatingRefId = Clear });
            db.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing { Id = Guid.NewGuid(), LensOptionId = lensId, TriggerCoatingRefId = Clear, PairedCoatingRefId = BlueBlock });
        });
        var leadId = SeedLead(l =>
        {
            l.LensRangeType = LensRangeType.LensSet;
            l.PresetCatalogueId = setId;
            l.SphereLeft = 2.50m;
            l.SphereRight = 2.50m;
            l.PresetPupilDistanceBucket = 2;
            l.CoatingPreferenceRefId = BlueBlock;
        });

        var html = await PageAsync(leadId);

        Assert.Equal(new[] { BlueBlock, Clear }.Select(g => g.ToString()).Order(), CheckedCoatings(html).Order());
        Assert.DoesNotContain("Comes with", html);
        Assert.DoesNotContain("data-locked=\"true\"", html);

        var effects = await factory.CreateAdminClient().GetStringAsync($"/Leads/Convert/{leadId}/coatings?left={lensId}");
        using var doc = JsonDocument.Parse(effects);
        Assert.All(doc.RootElement.GetProperty("effects").EnumerateObject(), effect => Assert.Empty(effect.Value.GetProperty("locks").EnumerateArray()));
    }

    [Fact]
    public async Task TheScriptReadsTheRulesFromThePage_NotFromItsOwnCopy()
    {
        // The cylinder/add option meaning "none", the buckets a children's frame allows, and the
        // lenses each left lens pairs with are all rendered by the server from Rules.
        var set = SeedLensSet();
        var leadId = SeedLead();

        var html = await PageAsync(leadId);

        Assert.Matches(new Regex("<select[^>]*name=\"Form.CylinderLeft\"[^>]*>.*?<option value=\"0[.,]00\"[^>]*data-none", RegexOptions.Singleline), html);
        Assert.Matches(new Regex("<select[^>]*name=\"Form.AddRight\"[^>]*>.*?<option value=\"0[.,]00\"[^>]*data-none", RegexOptions.Singleline), html);
        Assert.Matches(new Regex("<option value=\"2\" data-childrens-frame-allows=\"true\""), html);
        Assert.Matches(new Regex("<option value=\"3\" data-childrens-frame-allows=\"false\""), html);

        var data = Regex.Match(html, "<script type=\"application/json\" data-lens-sets>(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        using var doc = JsonDocument.Parse(data);
        var lenses = doc.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == set.Id).GetProperty("lenses").EnumerateArray()
            .ToDictionary(l => l.GetProperty("id").GetGuid(), l => l.GetProperty("pairsWith").EnumerateArray().Select(e => e.GetGuid()).Order().ToList());
        Assert.Equal(new[] { set.Plus250, set.Plus300 }.Order(), lenses[set.Plus250]);
        Assert.Equal([set.BifocalLens], lenses[set.BifocalLens]);
    }

    [Fact]
    public async Task ACustomPrescriptionOffersEveryCoating()
    {
        var leadId = SeedLead();

        var html = await PageAsync(leadId);

        var active = Query(db => db.ReferenceDataItems.Count(x => x.Category == ReferenceDataCategory.Coating && x.IsActive));
        Assert.Equal(active, OfferedCoatings(html).Count);
    }

    // ---- The custom range's shop-style form ----

    [Fact]
    public async Task TheCustomFormRendersLensTypeAsRadios_AndGatesAxisAndLensTypeOnTheirPowers()
    {
        var leadId = SeedLead();

        var html = await PageAsync(leadId);

        Assert.Matches(new Regex("<input[^>]*type=\"radio\"[^>]*name=\"Form.LensTypeRefId\""), html);
        Assert.DoesNotMatch(new Regex("<select[^>]*name=\"Form.LensTypeRefId\""), html);
        // Nothing chosen yet: no cylinder, no add — so both are hidden until the admin asks for them.
        Assert.Matches(new Regex("data-axis-field=\"left\"[^>]*hidden"), html);
        Assert.Matches(new Regex("data-lens-type-field[^>]*hidden"), html);
    }

    // ---- helpers ----

    private static string ErrorFor(string html, string field)
    {
        var match = Regex.Match(html, $"<span[^>]*data-valmsg-for=\"{Regex.Escape(field)}\"[^>]*>(.*?)</span>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No validation message slot for {field} on the page.");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string? SelectedValue(string html, string selectName)
    {
        var select = Regex.Match(html, $"<select[^>]*name=\"{Regex.Escape(selectName)}\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success, $"No select named {selectName} on the page.");
        var option = Regex.Match(select.Groups[1].Value, "<option(?=[^>]*selected=\"selected\")[^>]*value=\"([^\"]*)\"");
        return option.Success ? option.Groups[1].Value : null;
    }

    private static List<string> OptionValues(string html, string selectName)
    {
        var select = Regex.Match(html, $"<select[^>]*name=\"{Regex.Escape(selectName)}\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success, $"No select named {selectName} on the page.");
        return Regex.Matches(select.Groups[1].Value, "<option[^>]*value=\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>The coating checkboxes not hidden as un-offered.</summary>
    private static List<string> OfferedCoatings(string html) =>
        Regex.Matches(html, "<div[^>]*data-coating-item[^>]*data-coating-id=\"([^\"]+)\"([^>]*)>")
            .Where(m => !m.Groups[2].Value.Contains("hidden"))
            .Select(m => m.Groups[1].Value)
            .ToList();

    private static List<string> CheckedCoatings(string html) =>
        Regex.Matches(html, "<input[^>]*name=\"Form.CoatingRefIds\"[^>]*>")
            .Where(m => m.Value.Contains("checked=\"checked\""))
            .Select(m => Regex.Match(m.Value, "value=\"([^\"]+)\"").Groups[1].Value)
            .ToList();
}
