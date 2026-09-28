namespace DotGlasses.Application.Common;

/// <summary>
/// Claim type names shared between DotGlasses.Web (issues them, both for cookie sign-in and
/// JWT) and DotGlasses.Infrastructure's CurrentUserContext (reads them). Kept here, not in
/// Infrastructure, so Web doesn't need to reference Infrastructure just to know a claim name.
/// No claim carries an org's scope, level or path: those are re-read from the database on every
/// request (ADR-0006).
/// </summary>
public static class DotGlassesClaimTypes
{
    /// <summary>Field App tokens only: the OrganisationNode id of the current location. Only
    /// names it — whether it may be used is re-checked against the database on every request.</summary>
    public const string CurrentLocationId = "dotglasses:current_location_id";
}
