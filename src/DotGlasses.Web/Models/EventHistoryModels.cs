namespace DotGlasses.Web.Models;

public record SaleOrTestEvent(string Type, bool Custom, string Name, string Outlet, string Country, string Time);
public record LeadEvent(string Name, string PhoneMasked, string Outlet, string Reason, string Logged);
public record ReferralEvent(string Outlet, string Country, string Reason, string Time);

public class EventHistoryViewModel
{
    public required string ActiveTab { get; init; }
    public string? SearchQuery { get; init; }
    public IReadOnlyList<SaleOrTestEvent> Events { get; set; } = [];
    public IReadOnlyList<LeadEvent> Leads { get; set; } = [];
    public IReadOnlyList<ReferralEvent> Referrals { get; set; } = [];

    public int Page { get; init; } = 1;
    public int PageSize { get; init; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
