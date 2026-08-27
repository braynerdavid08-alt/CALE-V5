namespace Cale.BuildingBlocks.Domain.Email;

public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default);
}
