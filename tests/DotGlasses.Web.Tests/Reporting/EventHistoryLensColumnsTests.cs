using System.Net;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Reporting;

/// <summary>
/// Event History's lens columns, its "Training" badge, the Leads tab's "Aware of price" column,
/// and the same things in the CSV exports — where each lens value has a column of its own. The
/// screen and the export are fed by one row shape; these check both ends of it.
/// </summary>
public class EventHistoryLensColumnsTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;
    private static readonly Guid BlueBlock = ReferenceDataSeedConfiguration.CoatingBlueBlockId;
    private static readonly Guid Bifocal = ReferenceDataSeedConfiguration.LensTypeBifocalId;

    /// <summary>The seeded "Astigmatism" referral reason.</summary>
    private static readonly Guid AReferralReason = new("b0000000-0000-0000-0000-000000000032");

    private static int _nextSegment = 900;

    /// <summary>A retail point under the seeded Kenya retailer; optionally a training one, or one
    /// beneath a new training retailer.</summary>
    private (string Path, string Name) ARetailPoint(bool training = false, bool beneathATrainingRetailer = false)
    {
        var n = Interlocked.Increment(ref _nextSegment);
        var name = $"History Outlet {n}";
        string path;

        if (beneathATrainingRetailer)
        {
            var retailerId = Guid.NewGuid();
            var retailerPath = $"{OrganisationSeedConfiguration.KenyaPath}{n}/";
            path = $"{retailerPath}1/";
            factory.Seed(db => db.OrganisationNodes.AddRange(
                new OrganisationNode { Id = retailerId, ParentId = OrganisationSeedConfiguration.KenyaId, Name = $"History Training School {n}", Level = OrganisationLevel.Intermediate, HierarchyPath = retailerPath, IsTrainingOrg = true },
                new OrganisationNode { Id = Guid.NewGuid(), ParentId = retailerId, Name = name, Level = OrganisationLevel.RetailPoint, HierarchyPath = path }));
        }
        else
        {
            path = $"{OrganisationSeedConfiguration.KenyaRetailerPath}{n}/";
            factory.Seed(db => db.OrganisationNodes.Add(
                new OrganisationNode { Id = Guid.NewGuid(), ParentId = OrganisationSeedConfiguration.KenyaRetailerId, Name = name, Level = OrganisationLevel.RetailPoint, HierarchyPath = path, IsTrainingOrg = training }));
        }

        return (path, name);
    }

    /// <summary>A name no other row on the screen has, so a test can find its own.</summary>
    private static string AName() => $"Customer {Guid.NewGuid():N}"[..20];

    private Sale SeedSale(string path, string customerName, Action<Sale>? configure = null, params Guid[] coatings)
    {
        var customer = new Customer { Id = Guid.NewGuid(), HierarchyPath = path, FullName = customerName, PhoneNumber = "+254711000111", CreatedAtUtc = DateTimeOffset.UtcNow };
        var sale = new Sale { Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), CustomerId = customer.Id, ConsentGiven = true, CreatedAtUtc = DateTimeOffset.UtcNow };
        configure?.Invoke(sale);
        factory.Seed(db =>
        {
            db.Customers.Add(customer);
            db.Sales.Add(sale);
            db.SaleCoatings.AddRange(coatings.Select(c => new SaleCoating { Id = Guid.NewGuid(), SaleId = sale.Id, CoatingRefId = c, CreatedAtUtc = DateTimeOffset.UtcNow }));
        });
        return sale;
    }

    private Lead SeedLead(string path, string customerName, Action<Lead>? configure = null, params Guid[] orderedCoatings)
    {
        var customer = new Customer { Id = Guid.NewGuid(), HierarchyPath = path, FullName = customerName, PhoneNumber = "+254711000222", CreatedAtUtc = DateTimeOffset.UtcNow };
        var lead = new Lead { Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), CustomerId = customer.Id, ConsentGiven = true, CreatedAtUtc = DateTimeOffset.UtcNow };
        configure?.Invoke(lead);
        factory.Seed(db =>
        {
            db.Customers.Add(customer);
            db.Leads.Add(lead);
            db.LeadCoatings.AddRange(orderedCoatings.Select(c => new LeadCoating { Id = Guid.NewGuid(), LeadId = lead.Id, CoatingRefId = c, CreatedAtUtc = DateTimeOffset.UtcNow }));
        });
        return lead;
    }

    private static void ACustomLens(Sale sale)
    {
        sale.LensRangeType = LensRangeType.Custom;
        sale.SphereLeft = -1.25m;
        sale.CylinderLeft = -0.75m;
        sale.AxisLeft = 90m;
        sale.AddLeft = 2.00m;
        sale.SphereRight = 0.50m;
        sale.AddRight = 2.00m;
        sale.LensTypeRefId = Bifocal;
        sale.PupilDistanceMm = 62m;
    }

    private async Task<string> TabAsync(string tab, string search = "") =>
        WebUtility.HtmlDecode(await factory.CreateAdminClient().GetStringAsync($"/EventHistory?tab={tab}&search={Uri.EscapeDataString(search)}"));

    /// <summary>The cells of the one table row holding <paramref name="marker"/>, tags stripped.</summary>
    private static List<string> RowCells(string html, string marker)
    {
        var row = Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).SingleOrDefault(r => r.Contains(marker, StringComparison.Ordinal));
        Assert.True(row is not null, $"No single row holding \"{marker}\".");
        return Regex.Matches(row!, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline)
            .Select(m => Regex.Replace(Regex.Replace(m.Groups[1].Value, "<[^>]+>", " "), @"\s+", " ").Trim())
            .ToList();
    }

    private static List<string> Headers(string html) =>
        Regex.Matches(html, "<th>(.*?)</th>").Select(m => m.Groups[1].Value).ToList();

    private async Task<(List<string> Headers, List<Dictionary<string, string>> Rows)> ExportAsync(string tab)
    {
        var csv = (await factory.CreateAdminClient().GetStringAsync($"/EventHistory/Export?tab={tab}")).TrimStart('﻿');
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',').ToList();

        // Nothing these tests seed holds a comma, so a plain split is the row.
        var rows = lines.Skip(1)
            .Select(line => line.Split(','))
            .Where(cells => cells.Length == headers.Count)
            .Select(cells => headers.Zip(cells).ToDictionary(p => p.First, p => p.Second))
            .ToList();
        return (headers, rows);
    }

    // --- Sales tab ----------------------------------------------------------------------------

    [Fact]
    public async Task TheSalesTab_HasTheThreeLensColumns_LeftEyeFirst()
    {
        var headers = Headers(await TabAsync("sales"));

        var range = headers.IndexOf("Lens range");
        Assert.True(range >= 0);
        Assert.Equal(["Lens range", "Lens power LE", "Lens power RE"], headers.Skip(range).Take(3));
    }

    [Fact]
    public async Task ACustomSale_ShowsCustomAndEachEyesPower_InTheOneLensPowerFormat()
    {
        var (path, _) = ARetailPoint();
        var name = AName();
        SeedSale(path, name, ACustomLens);

        var cells = RowCells(await TabAsync("sales"), name);

        Assert.Contains("Custom", cells);
        Assert.Contains(LensPowerValues.FormatLensPower(-1.25m, -0.75m, 90m, 2.00m), cells);
        Assert.Contains(LensPowerValues.FormatLensPower(0.50m, null, null, 2.00m), cells);

        // The type badge is just "Sale" now that the range has a column of its own.
        Assert.Equal("Sale", cells[0]);
    }

    [Fact]
    public async Task ALensSetSale_ShowsTheLensSetsName()
    {
        var (path, _) = ARetailPoint();
        var name = AName();
        SeedSale(path, name, s =>
        {
            s.LensRangeType = LensRangeType.LensSet;
            s.PresetCatalogueId = ExampleLensSets.SixLensSetId;
            s.SphereLeft = s.SphereRight = 1.50m;
            s.PresetPupilDistanceBucket = 2;
        });

        var cells = RowCells(await TabAsync("sales"), name);

        Assert.Contains("6-Lens Set", cells);
        Assert.Equal(2, cells.Count(c => c == LensPowerValues.FormatLensPower(1.50m, null, null, null)));
    }

    [Fact]
    public async Task ARecordWithNoLensPower_ShowsADashInThePowerColumns()
    {
        var (path, _) = ARetailPoint();
        var name = AName();
        SeedSale(path, name, s => s.LensRangeType = LensRangeType.Custom);

        var html = await TabAsync("sales");
        var cells = RowCells(html, name);
        var headers = Headers(html);

        Assert.Equal("—", cells[headers.IndexOf("Lens power LE")]);
        Assert.Equal("—", cells[headers.IndexOf("Lens power RE")]);
    }

    // --- The Training badge -------------------------------------------------------------------

    [Fact]
    public async Task RowsAtATrainingRetailPoint_OrBeneathATrainingRetailer_CarryTheBadge_AndOthersDoNot()
    {
        var (ordinaryPath, _) = ARetailPoint();
        var (trainingPath, _) = ARetailPoint(training: true);
        var (beneathPath, _) = ARetailPoint(beneathATrainingRetailer: true);
        var (ordinary, training, beneath) = (AName(), AName(), AName());
        SeedSale(ordinaryPath, ordinary);
        SeedSale(trainingPath, training);
        SeedSale(beneathPath, beneath);

        var html = await TabAsync("sales");
        var outlet = Headers(html).IndexOf("Outlet");

        Assert.DoesNotContain("Training", RowCells(html, ordinary)[outlet]);
        Assert.EndsWith("Training", RowCells(html, training)[outlet]);
        Assert.EndsWith("Training", RowCells(html, beneath)[outlet]);
    }

    [Fact]
    public async Task TheBadgeIsOnEveryTabWithAnOutletColumn()
    {
        var (path, outletName) = ARetailPoint(training: true);
        var name = AName();
        SeedLead(path, name, l => { l.ReferredOrTreated = true; l.ReferralReasonRefId = AReferralReason; });
        factory.Seed(db => db.Tests.Add(new Test { Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow }));

        Assert.Contains(RowCells(await TabAsync("leads"), name), c => c == $"{outletName} Training");
        Assert.Contains(RowCells(await TabAsync("tests"), outletName), c => c == $"{outletName} Training");
        Assert.Contains(RowCells(await TabAsync("referrals"), outletName), c => c == $"{outletName} Training");
    }

    // --- Leads tab ----------------------------------------------------------------------------

    [Fact]
    public async Task TheLeadsTab_ShowsWhetherTheCustomerWasToldThePrice_AndTheLens()
    {
        var (path, _) = ARetailPoint();
        var (told, notTold, neverAsked) = (AName(), AName(), AName());
        SeedLead(path, told, l =>
        {
            l.CustomerToldPrice = true;
            l.LensRangeType = LensRangeType.Custom;
            l.SphereLeft = -2.00m;
            l.SphereRight = -2.25m;
        });
        SeedLead(path, notTold, l => l.CustomerToldPrice = false);
        SeedLead(path, neverAsked);

        var html = await TabAsync("leads");
        var headers = Headers(html);
        var aware = headers.IndexOf("Aware of price");
        Assert.True(aware >= 0);

        var toldCells = RowCells(html, told);
        Assert.Equal("Yes", toldCells[aware]);
        Assert.Equal("Custom", toldCells[headers.IndexOf("Lens range")]);
        Assert.Equal(LensPowerValues.FormatLensPower(-2.00m, null, null, null), toldCells[headers.IndexOf("Lens power LE")]);
        Assert.Equal(LensPowerValues.FormatLensPower(-2.25m, null, null, null), toldCells[headers.IndexOf("Lens power RE")]);

        Assert.Equal("No", RowCells(html, notTold)[aware]);

        // A Lead recorded before the question existed has no answer, and no lens either.
        var oldCells = RowCells(html, neverAsked);
        Assert.Equal("—", oldCells[aware]);
        Assert.Equal("—", oldCells[headers.IndexOf("Lens range")]);
        Assert.Equal("—", oldCells[headers.IndexOf("Lens power LE")]);
    }

    // --- The exports --------------------------------------------------------------------------

    [Fact]
    public async Task TheSalesExport_HoldsEachLensValueInItsOwnColumn_WithLensTypeCoatingsAndTheTrainingFlag()
    {
        var (path, outletName) = ARetailPoint(training: true);
        var name = AName();
        SeedSale(path, name, ACustomLens, Clear, BlueBlock);

        var (headers, rows) = await ExportAsync("sales");

        foreach (var column in new[] { "Lens range", "Sphere LE", "Cylinder LE", "Axis LE", "Add LE", "Sphere RE", "Cylinder RE", "Axis RE", "Add RE", "Lens type", "Coatings", "Training org" })
        {
            Assert.Contains(column, headers);
        }

        Assert.True(headers.IndexOf("Sphere LE") < headers.IndexOf("Sphere RE"));

        var row = Assert.Single(rows, r => r["Name"] == name);
        Assert.Equal(outletName, row["Outlet"]);
        Assert.Equal("Yes", row["Training org"]);
        Assert.Equal("Custom", row["Lens range"]);
        Assert.Equal(("-1.25", "-0.75", "90", "+2.00"), (row["Sphere LE"], row["Cylinder LE"], row["Axis LE"], row["Add LE"]));
        Assert.Equal(("+0.50", "", "", "+2.00"), (row["Sphere RE"], row["Cylinder RE"], row["Axis RE"], row["Add RE"]));
        Assert.Equal("Bifocal", row["Lens type"]);
        Assert.Equal(new[] { "Blue block", "Clear" }, row["Coatings"].Split("; ").Order());
    }

    [Fact]
    public async Task TheLeadsExport_AddsAwareOfPrice_AndAnOrderingLeadsCoatingSet()
    {
        var (path, _) = ARetailPoint();
        var (ordering, preferring) = (AName(), AName());
        SeedLead(path, ordering, l =>
        {
            l.CustomerToldPrice = true;
            l.LensRangeType = LensRangeType.Custom;
            l.SphereLeft = -2.00m;
            l.SphereRight = -2.25m;
        }, Clear, BlueBlock);
        SeedLead(path, preferring, l => { l.CustomerToldPrice = false; l.CoatingPreferenceRefId = BlueBlock; });

        var (headers, rows) = await ExportAsync("leads");

        Assert.Contains("Aware of price", headers);
        Assert.Contains("Training org", headers);

        var orderingRow = Assert.Single(rows, r => r["Name"] == ordering);
        Assert.Equal("Yes", orderingRow["Aware of price"]);
        Assert.Equal("No", orderingRow["Training org"]);
        Assert.Equal(("-2.00", "-2.25"), (orderingRow["Sphere LE"], orderingRow["Sphere RE"]));
        Assert.Equal(new[] { "Blue block", "Clear" }, orderingRow["Coatings"].Split("; ").Order());

        // A Lead that didn't order has, at most, its one preference.
        var preferringRow = Assert.Single(rows, r => r["Name"] == preferring);
        Assert.Equal("No", preferringRow["Aware of price"]);
        Assert.Equal("Blue block", preferringRow["Coatings"]);
        Assert.Equal("", preferringRow["Lens range"]);
    }

    [Fact]
    public async Task TheTestsAndReferralsExports_GainTheTrainingFlag()
    {
        var (path, outletName) = ARetailPoint(training: true);
        factory.Seed(db => db.Tests.Add(new Test
        {
            Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow,
            ReferredOrTreated = true, ReferralReasonRefId = AReferralReason,
        }));

        var tests = await ExportAsync("tests");
        var referrals = await ExportAsync("referrals");

        Assert.Equal("Yes", Assert.Single(tests.Rows, r => r["Outlet"] == outletName)["Training org"]);
        Assert.Equal("Yes", Assert.Single(referrals.Rows, r => r["Outlet"] == outletName)["Training org"]);
    }
}
