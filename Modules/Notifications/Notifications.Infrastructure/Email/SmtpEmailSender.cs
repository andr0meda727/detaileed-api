using System.Net.Mail;

namespace Notifications.Infrastructure.Email;

/// <summary>
/// Sends the app's emails over plain SMTP without login: Mailpit on a developer's machine (see compose.yaml).
/// Production uses <see cref="ResendEmailSender"/>. The content comes from <see cref="EmailTemplates"/>.
/// </summary>
public class SmtpEmailSender(EmailSettings settings) : IEmailSender
{
    public async Task SendInvitationEmailAsync(
        string to, string studioName, string acceptUrl, DateTime expiresAt, TimeZoneInfo timeZone)
    {
        using var message = Compose(to, EmailTemplates.Invitation(studioName, acceptUrl, expiresAt, timeZone));

        await SendAsync(message);
    }

    public async Task SendReportEmailAsync(string to, DateOnly dateFrom, DateOnly dateTo, byte[] pdfBytes)
    {
        using var message = Compose(to, EmailTemplates.BookingsReport(dateFrom, dateTo));
        message.Attachments.Add(new Attachment(
            new MemoryStream(pdfBytes), EmailTemplates.ReportFileName(dateFrom, dateTo), "application/pdf"));

        await SendAsync(message);
    }

    public async Task SendAsync(string to, EmailContent content)
    {
        using var message = Compose(to, content);

        await SendAsync(message);
    }

    private MailMessage Compose(string to, EmailContent content)
    {
        var message = new MailMessage
        {
            From = settings.Sender,
            Subject = content.Subject,
            Body = content.Html,
            IsBodyHtml = true
        };
        message.To.Add(to);
        return message;
    }

    private async Task SendAsync(MailMessage message)
    {
        using var client = new SmtpClient(settings.Host, settings.Port);

        await client.SendMailAsync(message);
    }
}
