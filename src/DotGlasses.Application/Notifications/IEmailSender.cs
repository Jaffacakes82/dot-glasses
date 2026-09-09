namespace DotGlasses.Application.Notifications;

/// <summary>One method for the one real use case today (User Directory's invite flow) rather
/// than a generic send-anything abstraction — will likely generalize once a second email use
/// case exists. AzureEmailSender is the real (Azure Communication Services) delivery, wired
/// whenever ACS is provisioned in a deployed environment; LoggingEmailSender is the local-dev/
/// unprovisioned fallback.</summary>
public interface IEmailSender
{
    Task SendPasswordSetupInviteAsync(string toEmail, string recipientName, string setPasswordUrl, CancellationToken cancellationToken = default);
}
