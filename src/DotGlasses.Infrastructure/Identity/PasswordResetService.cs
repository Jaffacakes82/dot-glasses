using DotGlasses.Application.Users;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Identity;

public class PasswordResetService(UserManager<ApplicationUser> userManager, DotGlassesDbContext dbContext) : IPasswordResetService
{
    public async Task<PasswordResetLink?> RequestAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || UserSuspension.IsSuspended(user.LockoutEnd))
        {
            return null;
        }

        // One conditional UPDATE is both the rate limit and its record: of any number of requests
        // arriving together, on any replica, exactly one moves the timestamp and so sends the
        // email. Committed here, before a token exists — nothing is emitted for a request that
        // wasn't counted.
        var now = DateTimeOffset.UtcNow;
        var notSince = now - IPasswordResetService.EmailInterval;
        var claimed = await dbContext.Users
            .Where(u => u.Id == user.Id && (u.PasswordResetEmailSentAtUtc == null || u.PasswordResetEmailSentAtUtc < notSince))
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.PasswordResetEmailSentAtUtc, now), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        // A data-protected payload over the user's security stamp, not a database write: it stops
        // working when the password changes (the stamp moves) and after the token lifespan.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return new PasswordResetLink(user.Id, user.Email ?? email.Trim(), user.DisplayName(fallback: "there"), token);
    }
}
