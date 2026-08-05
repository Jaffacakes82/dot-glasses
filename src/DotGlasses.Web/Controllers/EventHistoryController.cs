using DotGlasses.Application.Reporting;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class EventHistoryController(IEventHistoryQueryService eventHistoryQueryService) : Controller
{
    public async Task<IActionResult> Index(string tab = "sales", string? search = null, CancellationToken cancellationToken = default)
    {
        var model = new EventHistoryViewModel
        {
            ActiveTab = tab,
            SearchQuery = search,
            Events = tab switch
            {
                "sales" => (await eventHistoryQueryService.ListSalesAsync(cancellationToken)).Select(ToWebModel).ToList(),
                "tests" => (await eventHistoryQueryService.ListTestsAsync(cancellationToken)).Select(ToWebModel).ToList(),
                _ => [],
            },
            Leads = tab == "leads"
                ? (await eventHistoryQueryService.ListLeadsAsync(search, cancellationToken)).Select(ToWebModel).ToList()
                : [],
            Referrals = tab == "referrals"
                ? (await eventHistoryQueryService.ListReferralsAsync(cancellationToken)).Select(ToWebModel).ToList()
                : [],
        };

        return View(model);
    }

    private static SaleOrTestEvent ToWebModel(SaleOrTestEventRow row) =>
        new(row.Type, row.Custom, row.Name, row.Outlet, row.Country, FormatAbsolute(row.CreatedAtUtc));

    private static LeadEvent ToWebModel(LeadEventRow row) =>
        new(row.Name, row.PhoneMasked, row.Outlet, row.Reason, FormatRelative(row.CreatedAtUtc));

    private static ReferralEvent ToWebModel(ReferralEventRow row) =>
        new(row.Outlet, row.Country, row.Reason, FormatAbsolute(row.CreatedAtUtc));

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
