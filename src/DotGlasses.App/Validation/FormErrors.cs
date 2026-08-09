namespace DotGlasses.App.Validation;

/// <summary>
/// Field-keyed validation messages for the consultation forms, populated from two sources that
/// have to land in the same place: the app's own pre-submit checks, and the API's
/// ValidationProblemDetails response when a record is rejected.
///
/// Keys are the *request DTO property names* (e.g. "ReasonNotPurchasedRefId"), because that's
/// what the server sends back — using the same keys client-side means a server rejection renders
/// against the right control with no translation table to keep in sync.
///
/// Deliberately not Blazor's EditContext/DataAnnotations: the rules here are conditional on other
/// answers (referral fields only when Referred, hard-case colour only when a case was sold, an
/// entire branch of lens fields depending on the chosen range), which attribute-based validation
/// expresses badly. The server's FluentValidation rules are the source of truth; these mirror
/// them closely enough to stop a bad record ever being queued.
/// </summary>
public class FormErrors
{
    private readonly Dictionary<string, string> _errors = new(StringComparer.OrdinalIgnoreCase);

    public bool Any => _errors.Count > 0;

    public string? this[string field] => _errors.GetValueOrDefault(field);

    /// <summary>First error wins — a field showing two messages at once is noise, and the first
    /// check to fire is the most fundamental one.</summary>
    public void Add(string field, string message) => _errors.TryAdd(field, message);

    public void AddIf(bool condition, string field, string message)
    {
        if (condition)
        {
            Add(field, message);
        }
    }

    public void Clear() => _errors.Clear();

    /// <summary>
    /// Folds the API's response into the same bag. Server messages are appended rather than
    /// replacing client ones so nothing already on screen silently disappears, and any error the
    /// client has no control for (an unexpected key) is surfaced through
    /// <see cref="Unattributed"/> instead of being swallowed.
    /// </summary>
    public void Merge(IReadOnlyDictionary<string, string[]> serverErrors, IReadOnlyCollection<string> knownFields)
    {
        foreach (var (field, messages) in serverErrors)
        {
            var message = string.Join(" ", messages);
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (knownFields.Contains(field, StringComparer.OrdinalIgnoreCase))
            {
                Add(field, message);
            }
            else
            {
                Unattributed.Add($"{field}: {message}");
            }
        }
    }

    /// <summary>Server errors that don't map to a control on this form — shown as a summary at
    /// the top rather than dropped, so a rejection is never invisible.</summary>
    public List<string> Unattributed { get; } = [];
}
