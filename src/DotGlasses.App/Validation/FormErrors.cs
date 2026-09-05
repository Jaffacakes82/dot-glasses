using DotGlasses.Rules;

namespace DotGlasses.App.Validation;

/// <summary>
/// Field-keyed validation messages for the consultation forms, populated from two sources that
/// have to land in the same place: the pre-submit check, and the API's ValidationProblemDetails
/// response when a record is rejected.
///
/// Keys are the *request DTO property names* (e.g. "ReasonNotPurchasedRefId"), because that's
/// what the server sends back — using the same keys client-side means a server rejection renders
/// against the right control with no translation table to keep in sync.
///
/// Since ticket 13 the two sources are no longer merely consistent, they are the same code:
/// the pre-submit check is <c>ConsultationRules.Check</c>, which is what the create endpoint runs
/// too (ADR-0002). So both fold in through the same <see cref="Attribute"/> split — a key this
/// form has a control for renders against that control, and anything else lands in
/// <see cref="Unattributed"/> rather than being swallowed. A client failure with no control is
/// exactly as bad as a server one: it would leave the form blocked with nothing highlighted.
///
/// Deliberately not Blazor's EditContext/DataAnnotations: the rules are conditional on other
/// answers (referral fields only when Referred, hard-case colour only when a case was sold, an
/// entire branch of lens fields depending on the chosen range), which attribute-based validation
/// expresses badly.
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
    /// Folds the shared module's verdict into the bag before a record is ever queued. Taken as
    /// <see cref="RuleResult"/> rather than a dictionary because a single key legitimately carries
    /// more than one failure (a Coating preference can be both inactive and unavailable for the
    /// chosen lens), which a dictionary conversion would throw on; <see cref="Add(string, string)"/>'s
    /// first-error-wins is what picks between them.
    /// </summary>
    public void AddRuleFailures(RuleResult result, IReadOnlyCollection<string> knownFields)
    {
        foreach (var failure in result.Failures)
        {
            Attribute(failure.Key, failure.Message, knownFields);
        }
    }

    /// <summary>
    /// Folds the API's response into the same bag. Server messages are appended rather than
    /// replacing client ones so nothing already on screen silently disappears.
    /// </summary>
    public void Merge(IReadOnlyDictionary<string, string[]> serverErrors, IReadOnlyCollection<string> knownFields)
    {
        foreach (var (field, messages) in serverErrors)
        {
            Attribute(field, string.Join(" ", messages), knownFields);
        }
    }

    /// <summary>Renders against the control that produced it where this form has one, and into the
    /// summary band where it doesn't — the one place that decision is made, for both sources.</summary>
    private void Attribute(string field, string message, IReadOnlyCollection<string> knownFields)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
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

    /// <summary>Server errors that don't map to a control on this form — shown as a summary at
    /// the top rather than dropped, so a rejection is never invisible.</summary>
    public List<string> Unattributed { get; } = [];
}
