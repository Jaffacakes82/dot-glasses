using DotGlasses.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace DotGlasses.Infrastructure.Notifications;

/// <summary>[OPEN] Stub — logs what would have been sent and returns immediately; no real
/// delivery. Swap for a real Azure Communication Services implementation when that's wired up;
/// the invite-link token mechanics (UserAdminService) don't need to change at all when this
/// does.</summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendPasswordSetupInviteAsync(string toEmail, string recipientName, string setPasswordUrl, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Would send password-setup invite to {Email} ({RecipientName}): {SetPasswordUrl}", toEmail, recipientName, setPasswordUrl);
        return Task.CompletedTask;
    }
}
