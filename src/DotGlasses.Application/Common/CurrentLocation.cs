using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Common;

/// <summary>The org a Field App token names as where the technician is recording (CONTEXT.md:
/// <b>Current location</b>).</summary>
public sealed record CurrentLocation(Guid OrgNodeId, HierarchyPath Path, string Name);

/// <summary>Why a Field App request's current location can or can't be recorded at.</summary>
public enum CurrentLocationStatus
{
    /// <summary>An active retail point the user is directly assigned to.</summary>
    Valid,

    /// <summary>The token names no location (or one that doesn't exist).</summary>
    NoLocation,

    /// <summary>The user is no longer directly assigned to it.</summary>
    NoLongerAssigned,

    /// <summary>It is not Retail Point level.</summary>
    NotRetailPoint,

    /// <summary>It has been deactivated.</summary>
    Deactivated,
}

/// <summary>
/// A Field App request's current location, checked against the database on this request
/// (ADR-0006). <see cref="Location"/> is set whenever the token names a real org — valid or not,
/// so a refusal can name it — and <see cref="ValidLocation"/> only when it may be recorded at.
/// An invalid location never fails authentication; it narrows the request's scope to nothing and
/// makes the create endpoints refuse.
/// </summary>
public sealed record CurrentLocationCheck(CurrentLocationStatus Status, CurrentLocation? Location)
{
    public static CurrentLocationCheck NoLocation { get; } = new(CurrentLocationStatus.NoLocation, null);

    public bool IsValid => Status == CurrentLocationStatus.Valid;

    /// <summary>Where a record would be stamped, or null when there is no valid location.</summary>
    public CurrentLocation? ValidLocation => IsValid ? Location : null;

    /// <summary>
    /// What a create endpoint refuses with — null when valid (ticket 07, spec.md "Recording").
    /// NotRetailPoint reuses NoLocation's copy: telling a technician "that's not a retail point"
    /// is no more actionable than telling them to choose one, and CurrentLocationCheck.Of never
    /// produces it from anything the Field App itself offered, only from a tampered/stale token.
    /// The other two name the location, which Location guarantees is set whenever Status isn't
    /// NoLocation (CurrentLocationCheck.Of only returns a non-NoLocation status from a non-null
    /// candidate).
    /// </summary>
    public string? RefusalMessage => Status switch
    {
        CurrentLocationStatus.Valid => null,
        CurrentLocationStatus.NoLongerAssigned => $"You're no longer assigned to {Location!.Name} — ask your admin.",
        CurrentLocationStatus.Deactivated => $"{Location!.Name} has been deactivated.",
        _ => "Choose a retail point before recording.",
    };

    /// <summary>
    /// The one definition of an <b>eligible location</b>: an active, Retail Point level org the
    /// user is directly assigned to. "My orgs", sign-in, switching and the per-request recheck all
    /// ask it, so what the Field App offers and what the server accepts can't drift apart. A
    /// broad scope never widens it — an assignment above a retail point doesn't make the retail
    /// point eligible (ADR-0006).
    /// </summary>
    public static CurrentLocationCheck Of(LocationCandidate? org)
    {
        if (org is null)
        {
            return NoLocation;
        }

        var location = new CurrentLocation(org.OrgNodeId, org.Path, org.Name);
        var status = !org.IsDirectlyAssigned ? CurrentLocationStatus.NoLongerAssigned
            : org.Level != OrganisationLevel.RetailPoint ? CurrentLocationStatus.NotRetailPoint
            : !org.IsActive ? CurrentLocationStatus.Deactivated
            : CurrentLocationStatus.Valid;

        return new CurrentLocationCheck(status, location);
    }
}

/// <summary>An org as the eligibility rule sees it, read by IUserAccessLoader.</summary>
public sealed record LocationCandidate(
    Guid OrgNodeId, HierarchyPath Path, string Name, OrganisationLevel Level, bool IsActive, bool IsDirectlyAssigned);
