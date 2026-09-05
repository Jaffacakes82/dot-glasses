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

    public async Task<IActionResult> Index(string? tab = null, string? search = null, DateOnly? fromDate = null, DateOnly? toDate = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var activeTab = ParseTab(tab);
        var paging = new PageRequest(Math.Max(1, page), PageSize);
        var (fromUtc, toUtcExclusive) = DateRange.ToUtcRange(fromDate, toDate);

        var model = new EventHistoryViewModel
        {
            ActiveTab = RouteValue(activeTab),
            SearchQuery = search,
            FromDate = fromDate,
            ToDate = toDate,
            Page = paging.Page,
            PageSize = paging.PageSize,
        };

        switch (activeTab)
        {
            case EventHistoryTab.Tests:
                var tests = await eventHistoryQueryService.ListTestsAsync(fromUtc, toUtcExclusive, paging, cancellationToken);
                model.Events = tests.Rows.Select(ToWebModel).ToList();
                ApplyTotals(model, tests.TotalCount, paging);
                break;
            case EventHistoryTab.Leads:
                var leads = await eventHistoryQueryService.ListLeadsAsync(search, fromUtc, toUtcExclusive, paging, cancellationToken);
                model.Leads = leads.Rows.Select(ToWebModel).ToList();
                ApplyTotals(model, leads.TotalCount, paging);
                break;
            case EventHistoryTab.Referrals:
                var referrals = await eventHistoryQueryService.ListReferralsAsync(fromUtc, toUtcExclusive, paging, cancellationToken);
                model.Referrals = referrals.Rows.Select(ToWebModel).ToList();
                ApplyTotals(model, referrals.TotalCount, paging);
                break;
            default:
                var sales = await eventHistoryQueryService.ListSalesAsync(fromUtc, toUtcExclusive, paging, cancellationToken);
                model.Events = sales.Rows.Select(ToWebModel).ToList();
                ApplyTotals(model, sales.TotalCount, paging);
                break;
        }

        return View(model);
    }

    /// <summary>Calls exactly the methods Index calls, with paging omitted — same filters, same
    /// ordering, and (because it is one query rather than two) the same hierarchy scoping, so a
    /// user can never export rows they could not have seen on screen. The tab is resolved through
    /// the same ParseTab as Index, so the two actions cannot disagree about what a given ?tab=
    /// means: they used to, with the screen quietly rendering an empty Referrals table for an
    /// unrecognised value while the export returned 400.</summary>
    public async Task<IActionResult> Export(string? tab = null, string? search = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        var activeTab = ParseTab(tab);
        var (fromUtc, toUtcExclusive) = DateRange.ToUtcRange(fromDate, toDate);

        byte[] csv;
        switch (activeTab)
        {
            case EventHistoryTab.Tests:
                var tests = await eventHistoryQueryService.ListTestsAsync(fromUtc, toUtcExclusive, paging: null, cancellationToken);
                csv = CsvExport.Build(
                    ["Type", "Outlet", "Country", "Created"],
                    tests.Rows.Select(r => (IReadOnlyList<string?>)[r.Type, r.Outlet, r.Country, FormatCsvDate(r.CreatedAtUtc)]));
                break;
            case EventHistoryTab.Leads:
                var leads = await eventHistoryQueryService.ListLeadsAsync(search, fromUtc, toUtcExclusive, paging: null, cancellationToken);
                csv = CsvExport.Build(
                    ["Name", "Phone", "Outlet", "Reason", "ConsentGiven", "Created", "Converted"],
                    leads.Rows.Select(r => (IReadOnlyList<string?>)[r.Name, r.PhoneMasked, r.Outlet, r.Reason, r.ConsentGiven.ToString(), FormatCsvDate(r.CreatedAtUtc), r.ConvertedFlag.ToString()]));
                break;
            case EventHistoryTab.Referrals:
                var referrals = await eventHistoryQueryService.ListReferralsAsync(fromUtc, toUtcExclusive, paging: null, cancellationToken);
                csv = CsvExport.Build(
                    ["Outlet", "Country", "Reason", "Created"],
                    referrals.Rows.Select(r => (IReadOnlyList<string?>)[r.Outlet, r.Country, r.Reason, FormatCsvDate(r.CreatedAtUtc)]));
                break;
            default:
                var sales = await eventHistoryQueryService.ListSalesAsync(fromUtc, toUtcExclusive, paging: null, cancellationToken);
                csv = CsvExport.Build(
                    ["Type", "Custom", "Name", "Outlet", "Country", "Created", "ConsentGiven"],
                    sales.Rows.Select(r => (IReadOnlyList<string?>)[r.Type, r.Custom.ToString(), r.Name, r.Outlet, r.Country, FormatCsvDate(r.CreatedAtUtc), r.ConsentGiven?.ToString()]));
                break;
        }

        return File(csv, "text/csv", $"event-history-{RouteValue(activeTab)}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    /// <summary>The four screen tabs. An unrecognised ?tab= value resolves to the same tab an
    /// absent one does — Sales, which is what the two actions' own parameter default already
    /// said — rather than one action erroring and the other rendering something. It is parsed
    /// once so the screen and the export cannot answer that question differently, and the
    /// resolved value (not the raw query-string value) is what reaches the view's ActiveTab and
    /// the export's filename.</summary>
    private enum EventHistoryTab
    {
        Sales,
        Tests,
        Leads,
        Referrals,
    }

    private static EventHistoryTab ParseTab(string? tab) => tab switch
    {
        "tests" => EventHistoryTab.Tests,
        "leads" => EventHistoryTab.Leads,
        "referrals" => EventHistoryTab.Referrals,
        _ => EventHistoryTab.Sales,
    };

    private static string RouteValue(EventHistoryTab tab) => tab switch
    {
        EventHistoryTab.Tests => "tests",
        EventHistoryTab.Leads => "leads",
        EventHistoryTab.Referrals => "referrals",
        _ => "sales",
    };

    private static void ApplyTotals(EventHistoryViewModel model, int totalCount, PageRequest paging)
    {
        model.TotalCount = totalCount;
        model.TotalPages = paging.TotalPages(totalCount);
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
