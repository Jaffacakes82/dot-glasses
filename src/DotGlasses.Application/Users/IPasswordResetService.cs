namespace DotGlasses.Application.Users;

/// <summary>
/// "Forgot password?" for both sign-in pages. Anyone can ask, for any address, so the answer to
/// the person asking is always <see cref="Acknowledgement"/> — whether the address has an account
/// is never disclosed — and what this returns only decides whether an email goes out.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>The one message shown for every request, on the Admin Portal and the Field App.</summary>
    public const string Acknowledgement = "If that email has an account, we've sent a link.";

    /// <summary>How long a reset link works. It also stops working once the password changes.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(1);

    /// <summary>At most one reset email per account in this window, so the feature can't be used
    /// to flood someone's inbox.</summary>
    public static readonly TimeSpan EmailInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// What to email, or null when nothing should be sent: no account has this address, the
    /// account is suspended, or it was emailed within <see cref="EmailInterval"/>. An Active or an
    /// Invited account gets a link. The "last emailed" time is committed before this returns, so
    /// the email is only ever sent for a request that has already been counted.
    /// </summary>
    Task<PasswordResetLink?> RequestAsync(string? email, CancellationToken cancellationToken = default);
}

public record PasswordResetLink(Guid UserId, string Email, string RecipientName, string Token);
