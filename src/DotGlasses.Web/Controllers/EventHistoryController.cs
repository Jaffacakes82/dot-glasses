using System.Globalization;
using DotGlasses.Application.Reporting;
using DotGlasses.Web.Export;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class EventHistoryController(IEventHistoryQueryService eventHistoryQueryService) : Controller
{
    private const int PageSize = 25;

    public async Task<IActionResult> Index(string tab = "sales", string? search = null, DateOnly? fromDate = null, DateOnly? toDate = null, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var (fromUtc, toUtcExclusive) = DateRange.ToUtcRange(fromDate, toDate);

        var model = new EventHistoryViewModel
        {
            ActiveTab = tab,
            SearchQuery = search,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = PageSize,
        };

        switch (tab)
        {
            case "sales":
                var salesPage = await eventHistoryQueryService.ListSalesAsync(fromUtc, toUtcExclusive, page, PageSize, cancellationToken);
                model.Events = salesPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = salesPage.TotalCount;
                model.TotalPages = salesPage.TotalPages;
                break;
            case "tests":
                var testsPage = await eventHistoryQueryService.ListTestsAsync(fromUtc, toUtcExclusive, page, PageSize, cancellationToken);
                model.Events = testsPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = testsPage.TotalCount;
                model.TotalPages = testsPage.TotalPages;
                break;
            case "leads":
                var leadsPage = await eventHistoryQueryService.ListLeadsAsync(search, fromUtc, toUtcExclusive, page, PageSize, cancellationToken);
                model.Leads = leadsPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = leadsPage.TotalCount;
                model.TotalPages = leadsPage.TotalPages;
                break;
            case "referrals":
                var referralsPage = await eventHistoryQueryService.ListReferralsAsync(fromUtc, toUtcExclusive, page, PageSize, cancellationToken);
                model.Referrals = referralsPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = referralsPage.TotalCount;
                model.TotalPages = referralsPage.TotalPages;
                break;
        }

        return View(model);
    }

    /// <summary>Drives off the same ExportXAsync methods as Index's ListXAsync equivalents — same
    /// filter parameters, same scoping, just unpaged — so a user can never export rows they
    /// couldn't otherwise see on screen.</summary>
    public async Task<IActionResult> Export(string tab = "sales", string? search = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtcExclusive) = DateRange.ToUtcRange(fromDate, toDate);

        byte[] csv;
        switch (tab)
        {
            case "sales":
                var sales = await eventHistoryQueryService.ExportSalesAsync(fromUtc, toUtcExclusive, cancellationToken);
                csv = CsvExport.Build(
                    ["Type", "Custom", "Name", "Outlet", "Country", "Created", "ConsentGiven"],
                    sales.Select(r => (IReadOnlyList<string?>)[r.Type, r.Custom.ToString(), r.Name, r.Outlet, r.Country, FormatCsvDate(r.CreatedAtUtc), r.ConsentGiven?.ToString()]));
                break;
            case "tests":
                var tests = await eventHistoryQueryService.ExportTestsAsync(fromUtc, toUtcExclusive, cancellationToken);
                csv = CsvExport.Build(
                    ["Type", "Outlet", "Country", "Created"],
                    tests.Select(r => (IReadOnlyList<string?>)[r.Type, r.Outlet, r.Country, FormatCsvDate(r.CreatedAtUtc)]));
                break;
            case "leads":
                var leads = await eventHistoryQueryService.ExportLeadsAsync(search, fromUtc, toUtcExclusive, cancellationToken);
                csv = CsvExport.Build(
                    ["Name", "Phone", "Outlet", "Reason", "ConsentGiven", "Created", "Converted"],
                    leads.Select(r => (IReadOnlyList<string?>)[r.Name, r.PhoneMasked, r.Outlet, r.Reason, r.ConsentGiven.ToString(), FormatCsvDate(r.CreatedAtUtc), r.ConvertedFlag.ToString()]));
                break;
            case "referrals":
                var referrals = await eventHistoryQueryService.ExportReferralsAsync(fromUtc, toUtcExclusive, cancellationToken);
                csv = CsvExport.Build(
                    ["Outlet", "Country", "Reason", "Created"],
                    referrals.Select(r => (IReadOnlyList<string?>)[r.Outlet, r.Country, r.Reason, FormatCsvDate(r.CreatedAtUtc)]));
                break;
            default:
                return BadRequest();
        }

        return File(csv, "text/csv", $"event-history-{tab}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static string FormatCsvDate(DateTimeOffset timestamp) => timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static SaleOrTestEvent ToWebModel(SaleOrTestEventRow row) =>
        new(row.Type, row.Custom, row.Name, row.Outlet, row.Country, FormatAbsolute(row.CreatedAtUtc), row.ConsentGiven);

    private static LeadEvent ToWebModel(LeadEventRow row) =>
        new(row.Id, row.Name, row.PhoneMasked, row.Outlet, row.Reason, FormatRelative(row.CreatedAtUtc), row.ConsentGiven, row.ConvertedFlag);

    private static ReferralEvent ToWebModel(ReferralEventRow row) =>
        new(row.Source, row.Outlet, row.Country, row.Reason, row.TreatedInFacility, FormatAbsolute(row.CreatedAtUtc));

    private static string FormatAbsolute(DateTimeOffset timestamp) => timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private static string FormatRelative(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            var minutes = (int)elapsed.TotalMinutes;
            return $"{minutes} minute{(minutes == 1 ? "" : "s")} ago";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = (int)elapsed.TotalHours;
            return $"{hours} hour{(hours == 1 ? "" : "s")} ago";
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            var days = (int)elapsed.TotalDays;
            return $"{days} day{(days == 1 ? "" : "s")} ago";
        }

        return FormatAbsolute(timestamp);
    }
}
