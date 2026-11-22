using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Notifications.Infrastructure.Email;

public enum EmailProvider
{
    /// <summary>Plain SMTP without login, for Mailpit on a developer's machine (see compose.yaml).</summary>
    Smtp,

    /// <summary>Resend's HTTP API, used in production (see docs/email.md).</summary>
    Resend
}

/// <summary>
/// The <c>Email</c> configuration section. It is read and checked once at startup, so a missing setting stops the
/// host instead of failing the first email much later inside a consumer.
/// </summary>
public sealed record EmailSettings(
    EmailProvider Provider, string From, string FromName, string? Host, int Port, string? ApiKey)
{
    /// <summary>The sender as an address header, e.g. <c>"Detaileed" &lt;noreply@detaileed.com&gt;</c>.</summary>
    public MailAddress Sender => new(From, FromName);

    public static EmailSettings Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("Email");

        var providerName = section["Provider"];
        if (!Enum.TryParse<EmailProvider>(providerName, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException(
                $"Email:Provider must be one of {string.Join(", ", Enum.GetNames<EmailProvider>())}, " +
                $"not '{providerName}'.");
        }

        var settings = new EmailSettings(
            provider,
            Required(section, "From"),
            section["FromName"] ?? "Detaileed",
            section["Host"],
            int.TryParse(section["Port"], out var port) ? port : 1025,
            section["ApiKey"]);

        switch (provider)
        {
            case EmailProvider.Smtp:
                Required(section, "Host");
                break;
            case EmailProvider.Resend:
                Required(section, "ApiKey");
                break;
        }

        return settings;
    }

    private static string Required(IConfigurationSection section, string key) =>
        string.IsNullOrWhiteSpace(section[key])
            ? throw new InvalidOperationException($"Email:{key} is missing.")
            : section[key]!;
}
