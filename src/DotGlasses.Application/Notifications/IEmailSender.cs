namespace DotGlasses.Application.Notifications;

/// <summary>One method for the one real use case today (User Directory's invite flow) rather
/// than a generic send-anything abstraction — will likely generalize once a second email use
/// case exists. Real delivery (Azure Communication Services) is not implemented yet; see
/// LoggingEmailSender.</summary>
public interface IEmailSender
{
    Task SendPasswordSetupInviteAsync(string toEmail, string recipientName, string setPasswordUrl, CancellationToken cancellationToken = default);
}
