namespace DotGlasses.Infrastructure.Identity;

/// <summary>
/// Suspension is stored in Identity's lockout columns, which it shares with sign-in's own
/// temporary lockout after repeated wrong passwords (lockoutOnFailure: true). The two must never
/// be confused: anyone who knows a username can trigger the temporary one, and treating it as a
/// suspension would let them sign the real user out and fail their Field App token — a 401 the
/// Field App's outbox treats as a permanent rejection. So a suspension is marked by a lockout that
/// never ends, and only that counts.
/// </summary>
public static class UserSuspension
{
    /// <summary>The LockoutEnd a suspension writes.</summary>
    public static readonly DateTimeOffset LockoutEnd = DateTimeOffset.MaxValue;

    /// <summary>
    /// Compared against a threshold a year short of the sentinel, not for equality: Postgres
    /// timestamptz keeps microseconds while DateTimeOffset.MaxValue carries 100ns ticks, so the
    /// value read back is not the value written. No temporary lockout comes anywhere near it.
    /// </summary>
    private static readonly DateTimeOffset Threshold = LockoutEnd.AddYears(-1);

    public static bool IsSuspended(DateTimeOffset? lockoutEnd) => lockoutEnd >= Threshold;
}
