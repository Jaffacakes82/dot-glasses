using DotGlasses.Application.Reporting;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class EventHistoryController(IEventHistoryQueryService eventHistoryQueryService) : Controller
{
    private const int PageSize = 25;

    public async Task<IActionResult> Index(string tab = "sales", string? search = null, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);

        var model = new EventHistoryViewModel
        {
            ActiveTab = tab,
            SearchQuery = search,
            Page = page,
            PageSize = PageSize,
        };

        switch (tab)
        {
            case "sales":
                var salesPage = await eventHistoryQueryService.ListSalesAsync(page, PageSize, cancellationToken);
                model.Events = salesPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = salesPage.TotalCount;
                model.TotalPages = salesPage.TotalPages;
                break;
            case "tests":
                var testsPage = await eventHistoryQueryService.ListTestsAsync(page, PageSize, cancellationToken);
                model.Events = testsPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = testsPage.TotalCount;
                model.TotalPages = testsPage.TotalPages;
                break;
            case "leads":
                var leadsPage = await eventHistoryQueryService.ListLeadsAsync(search, page, PageSize, cancellationToken);
                model.Leads = leadsPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = leadsPage.TotalCount;
                model.TotalPages = leadsPage.TotalPages;
                break;
            case "referrals":
                var referralsPage = await eventHistoryQueryService.ListReferralsAsync(page, PageSize, cancellationToken);
                model.Referrals = referralsPage.Items.Select(ToWebModel).ToList();
                model.TotalCount = referralsPage.TotalCount;
                model.TotalPages = referralsPage.TotalPages;
                break;
        }

        return View(model);
    }

    private static SaleOrTestEvent ToWebModel(SaleOrTestEventRow row) =>
        new(row.Type, row.Custom, row.Name, row.Outlet, row.Country, FormatAbsolute(row.CreatedAtUtc), row.ConsentGiven);

    private static LeadEvent ToWebModel(LeadEventRow row) =>
        new(row.Id, row.Name, row.PhoneMasked, row.Outlet, row.Reason, FormatRelative(row.CreatedAtUtc), row.ConsentGiven, row.ConvertedFlag);

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
