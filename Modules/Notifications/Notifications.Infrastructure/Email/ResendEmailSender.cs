using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notifications.Infrastructure.Email;

/// <summary>
/// Sends the app's emails through Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).
/// A refused email throws, so the consumer's message is retried and, failing that, lands in the error queue.
/// </summary>
public sealed class ResendEmailSender(HttpClient http, EmailSettings settings) : IEmailSender
{
    public const string Endpoint = "https://api.resend.com/emails";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task SendInvitationEmailAsync(
        string to, string studioName, string acceptUrl, DateTime expiresAt, TimeZoneInfo timeZone) =>
        SendAsync(to, EmailTemplates.Invitation(studioName, acceptUrl, expiresAt, timeZone), attachments: null);

    public Task SendReportEmailAsync(string to, DateOnly dateFrom, DateOnly dateTo, byte[] pdfBytes) =>
        SendAsync(to, EmailTemplates.BookingsReport(dateFrom, dateTo),
            [new ResendAttachment(EmailTemplates.ReportFileName(dateFrom, dateTo), Convert.ToBase64String(pdfBytes))]);

    public Task SendAsync(string to, EmailContent content) => SendAsync(to, content, attachments: null);

    private async Task SendAsync(string to, EmailContent content, IReadOnlyList<ResendAttachment>? attachments)
    {
        var email = new ResendEmail(settings.Sender.ToString(), [to], content.Subject, content.Html, attachments);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(email, options: Json);

        using var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) return;

        // Resend explains the refusal in the body, e.g. an unverified domain or the rate limit
        var reason = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Resend refused the email ({(int)response.StatusCode}): {Truncate(reason, 500)}",
            inner: null, response.StatusCode);
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    private sealed record ResendEmail(
        string From, IReadOnlyList<string> To, string Subject, string Html, IReadOnlyList<ResendAttachment>? Attachments);

    private sealed record ResendAttachment(string Filename, string Content);
}
