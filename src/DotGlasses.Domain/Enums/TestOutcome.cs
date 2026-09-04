namespace DotGlasses.Domain.Enums;

/// <summary>Stays a 2-value enum, not a bool — leaves room for a future third outcome at no
/// extra cost. "Referred" was retired as an outcome value (2026-09-03): "referred or treated" is
/// now an orthogonal flag (Test.ReferredOrTreated etc.), independently captured on Test/Lead/Sale
/// at creation time rather than tied to one particular outcome — see the "Referred or treated"
/// entry in CONTEXT.md.</summary>
public enum TestOutcome
{
    NoGlassesNeeded = 0,
    NeedsGlasses = 1,
}
