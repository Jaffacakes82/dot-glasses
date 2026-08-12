using Azure;
using Azure.Communication.Email;
using DotGlasses.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace DotGlasses.Infrastructure.Notifications;

/// <summary>
/// Real delivery via Azure Communication Services' Email service — replaces LoggingEmailSender
/// once ACS_CONNECTION_STRING/ACS_SENDER_DOMAIN are present in configuration (see
/// DependencyInjection.AddInfrastructure). Those two values only ever exist in a deployed
/// environment: AppHost.cs only provisions the underlying acs.bicep resource and injects them as
/// environment variables when builder.ExecutionContext.IsPublishMode is true, so local `dotnet
/// run` always falls back to LoggingEmailSender regardless of this class existing.
///
/// Deliberately never throws — UserDirectoryController awaits SendPasswordSetupInviteAsync with
/// no try/catch and unconditionally shows the raw set-password link via TempData regardless of
/// whether the email actually sent, exactly as it already did with LoggingEmailSender. A real SMTP/
/// API delivery failure (bad credentials, transient outage, throttling) must not turn into a 500
/// on an otherwise-successful invite/reset — the shown link is the fallback for exactly that case,
/// not just for the pre-ACS era.
/// </summary>
public class AzureEmailSender(EmailClient emailClient, string senderAddress, ILogger<AzureEmailSender> logger) : IEmailSender
{
    public async Task SendPasswordSetupInviteAsync(string toEmail, string recipientName, string setPasswordUrl, CancellationToken cancellationToken = default)
    {
        var content = new EmailContent("Set your DOT Glasses password")
        {
            PlainText = $"Hi {recipientName},\n\n"
                + "You've been invited to the DOT Glasses platform. Set your password using the link below:\n\n"
                + $"{setPasswordUrl}\n\n"
                + "If you weren't expecting this invitation, you can ignore this email.",
        };

        var message = new EmailMessage(senderAddress, toEmail, content);

        try
        {
            await emailClient.SendAsync(WaitUntil.Started, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password-setup invite to {Email} via Azure Communication Services — the set-password link is still shown in the Admin Portal for manual relay.", toEmail);
        }
    }
}
