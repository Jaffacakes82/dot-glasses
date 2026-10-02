namespace DotGlasses.Web.Models;

/// <summary>The three lens columns the Sales and Leads tabs show: the lens range, then each eye's
/// lens power, left eye first, each already in its display text ("—" where there is none).</summary>
public record LensColumns(string Range, string Left, string Right)
{
    public const string None = "—";
}

public record SaleOrTestEvent(string Type, bool Custom, string? Name, string Outlet, string Country, string Time, bool? ConsentGiven, bool IsTraining, LensColumns Lens);
public record LeadEvent(Guid Id, string Name, string PhoneMasked, string Outlet, string Reason, string Logged, bool ConsentGiven, bool ConvertedFlag, bool IsTraining, LensColumns Lens, string AwareOfPrice);
public record ReferralEvent(string Source, string Outlet, string Country, string Reason, bool TreatedInFacility, string Time, bool IsTraining);

public class EventHistoryViewModel
{
    public required string ActiveTab { get; init; }
    public string? SearchQuery { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public IReadOnlyList<SaleOrTestEvent> Events { get; set; } = [];
    public IReadOnlyList<LeadEvent> Leads { get; set; } = [];
    public IReadOnlyList<ReferralEvent> Referrals { get; set; } = [];

    public int Page { get; init; } = 1;
    public int PageSize { get; init; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
