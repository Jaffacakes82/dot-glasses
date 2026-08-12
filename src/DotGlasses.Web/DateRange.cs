namespace DotGlasses.Web;

/// <summary>Converts a Web-layer inclusive DateOnly?/DateOnly? filter pair into the
/// [fromUtc, toUtcExclusive) instant range the Application-layer query services expect.
/// Shared by HomeController and EventHistoryController so a Dashboard drill-down link and
/// Event History's own filter agree on exactly the same day boundaries. Deliberately treats
/// the picked dates as UTC-day boundaries rather than adjusting for the viewer's local
/// timezone — a reasonable simplification (Event History's own row timestamps are already
/// shown converted to local time for display, this is only the filter boundary), not
/// something worth a per-user timezone lookup for.</summary>
public static class DateRange
{
    public static (DateTimeOffset? FromUtc, DateTimeOffset? ToUtcExclusive) ToUtcRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var fromUtc = fromDate.HasValue ? new DateTimeOffset(fromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        var toUtcExclusive = toDate.HasValue ? new DateTimeOffset(toDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1) : (DateTimeOffset?)null;
        return (fromUtc, toUtcExclusive);
    }
}
