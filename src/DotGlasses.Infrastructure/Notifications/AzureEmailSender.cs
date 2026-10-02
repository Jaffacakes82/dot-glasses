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
        var content = new EmailContent("Set your Dot Glasses password")
        {
            PlainText = $"Hi {recipientName},\n\n"
                + "You've been invited to the Dot Glasses platform. Set your password using the link below:\n\n"
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

    public async Task SendPasswordResetAsync(string toEmail, string recipientName, string resetUrl, CancellationToken cancellationToken = default)
    {
        var content = new EmailContent(PasswordResetEmail.Subject)
        {
            PlainText = PasswordResetEmail.Body(recipientName, resetUrl),
        };

        try
        {
            await emailClient.SendAsync(WaitUntil.Started, new EmailMessage(senderAddress, toEmail, content), cancellationToken);
        }
        catch (Exception ex)
        {
            // Never thrown onwards: the person asking is told the same thing whether or not an
            // email went out, and an admin's Reset password button is the fallback.
            logger.LogError(ex, "Failed to send password-reset email to {Email} via Azure Communication Services.", toEmail);
        }
    }
}

/// <summary>The wording of the "Forgot password?" email, kept apart from the sender so it can be
/// read and tested without a mail client.</summary>
public static class PasswordResetEmail
{
    public const string Subject = "Reset your Dot Glasses password";

    public static string Body(string recipientName, string resetUrl) =>
        $"Hi {recipientName},\n\n"
        + "Someone asked to reset the password for your Dot Glasses account. Set a new one using the link below:\n\n"
        + $"{resetUrl}\n\n"
        + "The link works for 24 hours, and stops working once you have set a new password.\n\n"
        + "If you didn't ask for this, you can ignore this email and your password stays as it is.";
}
