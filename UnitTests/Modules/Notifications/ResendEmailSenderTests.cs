using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Notifications.Infrastructure.Email;

namespace Unit.Modules.Notifications;

[TestFixture]
public class ResendEmailSenderTests
{
    private static readonly EmailSettings Settings =
        new(EmailProvider.Resend, "noreply@detaileed.com", "Detaileed", Host: null, Port: 0, ApiKey: "re_test");

    private sealed class RecordingHandler(HttpStatusCode status, string body = "{\"id\":\"1\"}") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public JsonElement Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static (ResendEmailSender, RecordingHandler) Sender(HttpStatusCode status = HttpStatusCode.OK,
        string body = "{\"id\":\"1\"}")
    {
        var handler = new RecordingHandler(status, body);
        return (new ResendEmailSender(new HttpClient(handler), Settings), handler);
    }

    [Test]
    public async Task SendAsync_PostsTheEmailWithTheApiKey()
    {
        var (sender, handler) = Sender();

        await sender.SendAsync("anna@example.com", new EmailContent("Temat", "<p>Treść</p>"));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Request!.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Request.RequestUri, Is.EqualTo(new Uri(ResendEmailSender.Endpoint)));
            Assert.That(handler.Request.Headers.Authorization?.ToString(), Is.EqualTo("Bearer re_test"));
            Assert.That(handler.Body.GetProperty("from").GetString(), Is.EqualTo("\"Detaileed\" <noreply@detaileed.com>"));
            Assert.That(handler.Body.GetProperty("to")[0].GetString(), Is.EqualTo("anna@example.com"));
            Assert.That(handler.Body.GetProperty("subject").GetString(), Is.EqualTo("Temat"));
            Assert.That(handler.Body.GetProperty("html").GetString(), Is.EqualTo("<p>Treść</p>"));
            Assert.That(handler.Body.TryGetProperty("attachments", out _), Is.False);
        });
    }

    [Test]
    public async Task SendReportEmailAsync_AttachesThePdfInBase64()
    {
        var (sender, handler) = Sender();
        byte[] pdf = [0x25, 0x50, 0x44, 0x46];

        await sender.SendReportEmailAsync("owner@example.com", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), pdf);

        var attachment = handler.Body.GetProperty("attachments")[0];
        Assert.Multiple(() =>
        {
            Assert.That(attachment.GetProperty("filename").GetString(), Is.EqualTo("raport_20261101_20261130.pdf"));
            Assert.That(Convert.FromBase64String(attachment.GetProperty("content").GetString()!), Is.EqualTo(pdf));
        });
    }

    [Test]
    public void SendAsync_ThrowsWhenResendRefuses()
    {
        var (sender, _) = Sender(HttpStatusCode.Forbidden, "{\"message\":\"The detaileed.com domain is not verified.\"}");

        var error = Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendAsync("anna@example.com", new EmailContent("Temat", "<p>Treść</p>")));

        Assert.That(error!.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(error.Message, Does.Contain("not verified"));
    }
}

[TestFixture]
public class EmailSettingsTests
{
    private static EmailSettings Read(params (string Key, string Value)[] values) =>
        EmailSettings.Read(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => KeyValuePair.Create($"Email:{v.Key}", (string?)v.Value)))
            .Build());

    [Test]
    public void Read_Resend()
    {
        var settings = Read(("Provider", "Resend"), ("From", "noreply@detaileed.com"), ("ApiKey", "re_test"));

        Assert.That(settings, Is.EqualTo(new EmailSettings(
            EmailProvider.Resend, "noreply@detaileed.com", "Detaileed", null, 1025, "re_test")));
    }

    [Test]
    public void Read_ResendWithoutApiKey_Throws() =>
        Assert.That(() => Read(("Provider", "Resend"), ("From", "noreply@detaileed.com")),
            Throws.InvalidOperationException.With.Message.Contains("Email:ApiKey"));

    [Test]
    public void Read_SmtpWithoutHost_Throws() =>
        Assert.That(() => Read(("Provider", "Smtp"), ("From", "noreply@detaileed.com")),
            Throws.InvalidOperationException.With.Message.Contains("Email:Host"));

    [Test]
    public void Read_UnknownProvider_Throws() =>
        Assert.That(() => Read(("Provider", "Sendgrid"), ("From", "noreply@detaileed.com")),
            Throws.InvalidOperationException.With.Message.Contains("Email:Provider"));

    [Test]
    public void Read_WithoutFrom_Throws() =>
        Assert.That(() => Read(("Provider", "Smtp"), ("Host", "localhost")),
            Throws.InvalidOperationException.With.Message.Contains("Email:From"));
}
