namespace DotGlasses.Web.Models;

public record SaleOrTestEvent(string Type, bool Custom, string Name, string Outlet, string Country, string Time);
public record LeadEvent(string Name, string PhoneMasked, string Outlet, string Reason, string Logged);
public record ReferralEvent(string Outlet, string Country, string Reason, string Time);

public class EventHistoryViewModel
{
    public required string ActiveTab { get; init; }
    public required IReadOnlyList<SaleOrTestEvent> Events { get; init; }
    public required IReadOnlyList<LeadEvent> Leads { get; init; }
    public required IReadOnlyList<ReferralEvent> Referrals { get; init; }
}
