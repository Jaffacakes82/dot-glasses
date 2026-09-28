using DotGlasses.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace DotGlasses.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>The old single "active org". It no longer decides scope or permissions — those come
    /// from the user's org assignments, re-read every request (ADR-0006) — but it still counts as
    /// one of those assignments until the migration that removes these three columns backfills an
    /// assignment row for it, and the consumers not yet moved onto the combined scope still read
    /// it (via the claims below).</summary>
    public Guid? OrgNodeId { get; set; }

    /// <summary>Materialized path of OrgNodeId, e.g. "/1/4/", copied onto the HierarchyPath claim
    /// at sign-in. Read only by the single-org consumers (see OrgNodeId).</summary>
    public string HierarchyPath { get; set; } = string.Empty;

    /// <summary>Denormalized OrganisationNode.Level of OrgNodeId, copied onto the OrgLevel claim at
    /// sign-in. Read only by the single-org consumers (see OrgNodeId); level-gated policies use
    /// the highest assigned level instead.</summary>
    public OrganisationLevel? OrgLevel { get; set; }

    /// <summary>Stamped on every successful sign-in, both the MVC cookie path (AccountController)
    /// and the API JWT path (AuthController) — a RetailPoint User almost never touches the Admin
    /// Portal, so only stamping the cookie path would leave this permanently null for most users.</summary>
    public DateTimeOffset? LastLoginUtc { get; set; }

    /// <summary>Nullable — the three DevUserSeeder dev accounts predate this field and have
    /// none; User Directory falls back to UserName/Email for display when absent.</summary>
    public string? FullName { get; set; }

    /// <summary>The one fallback rule for "what do we call this user" — every caller-facing
    /// display of a user's name needs it, parameterized only by what "unset" should render as in
    /// that context (an admin table cell wants "—"; a technician's own device greeting wants
    /// nothing at all rather than a literal em dash).</summary>
    public string DisplayName(string fallback = "—") =>
        string.IsNullOrWhiteSpace(FullName) ? UserName ?? fallback : FullName;
}
