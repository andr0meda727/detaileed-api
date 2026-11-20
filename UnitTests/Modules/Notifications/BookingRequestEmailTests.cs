using Identity.Contracts;
using MassTransit;
using Notifications.Infrastructure.Consumers;
using Notifications.Infrastructure.Email;
using NSubstitute;
using Scheduling.Contracts;
using Shared.Kernel.Time;

namespace Unit.Modules.Notifications;

[TestFixture]
public class BookingRequestEmailTests
{
    // 08:00 UTC in November is 09:00 in Warsaw
    private static readonly DateTime AtUtc = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo Warsaw = TimeZones.Find("Europe/Warsaw");
    private readonly Guid _studioId = Guid.NewGuid();
    private IEmailSender _email = null!;
    private IStudioDirectory _studios = null!;
    private readonly EmailLinks _links = new("https://app.test/", "https://web.test");

    [SetUp]
    public void SetUp()
    {
        _email = Substitute.For<IEmailSender>();
        _studios = Substitute.For<IStudioDirectory>();
        _studios.FindTimeZoneAsync(_studioId, Arg.Any<CancellationToken>()).Returns("Europe/Warsaw");
        _studios.GetOwnerEmailsAsync(_studioId, Arg.Any<CancellationToken>()).Returns(["a@studio.pl", "b@studio.pl"]);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        return context;
    }

    [Test]
    public void Received_ShowsTheLocalTimeAndEncodesWhatTheCustomerTyped()
    {
        var email = EmailTemplates.BookingRequestReceived(
            "<b>Anna</b>", "500600700", "KR 1", ["Mycie & wosk"], AtUtc, Warsaw, "https://app.test/r");

        Assert.That(email.Subject, Does.Contain("20.11.2026 09:00"));
        Assert.That(email.Html, Does.Contain("&lt;b&gt;Anna&lt;/b&gt;"));
        Assert.That(email.Html, Does.Contain("<li>Mycie &amp; wosk</li>"));
        Assert.That(email.Html, Does.Not.Contain("<b>Anna</b>"));
    }

    [Test]
    public void Rejected_IncludesTheReasonOnlyWhenGiven()
    {
        Assert.That(EmailTemplates.BookingRequestRejected("Anna", "Studio", "Brak terminów", "https://web.test/b").Html, Does.Contain("Brak terminów"));
        Assert.That(EmailTemplates.BookingRequestRejected("Anna", "Studio", null, "https://web.test/b").Html, Does.Not.Contain("Powód"));
    }

    [Test]
    public async Task ANewRequest_IsEmailedToEveryOwner()
    {
        var requestId = Guid.NewGuid();
        await new BookingRequestEmailConsumer(_email, _studios, _links).Consume(Context(new BookingRequestSubmitted(
            _studioId, requestId, "Studio", "Anna", "500600700", "KR 1", AtUtc, ["Mycie"])));

        // The trailing slash of the base URL isn't doubled
        await _email.Received(1).SendAsync("a@studio.pl", Arg.Is<EmailContent>(c => c.Subject.Contains("09:00")
            && c.Html.Contains($"href=\"https://app.test/s/{_studioId}/requests?id={requestId}\"")));
        await _email.Received(1).SendAsync("b@studio.pl", Arg.Any<EmailContent>());
    }

    [Test]
    public async Task TheAnswer_IsEmailedToTheCustomer()
    {
        var consumer = new BookingRequestEmailConsumer(_email, _studios, _links);

        await consumer.Consume(Context(new BookingRequestDecided(
            _studioId, Guid.NewGuid(), "Detail Studio", "Anna", "anna@example.com", true, AtUtc, null)));
        await consumer.Consume(Context(new BookingRequestDecided(
            _studioId, Guid.NewGuid(), "Detail Studio", "Anna", "anna@example.com", false, null, "Brak terminów")));

        await _email.Received(1).SendAsync("anna@example.com",
            Arg.Is<EmailContent>(c => c.Subject.Contains("potwierdza") && c.Html.Contains("20.11.2026 09:00")
                && c.Html.Contains("href=\"https://web.test/account/bookings\"")));
        await _email.Received(1).SendAsync("anna@example.com",
            Arg.Is<EmailContent>(c => c.Subject.Contains("nie może") && c.Html.Contains("Brak terminów")
                && c.Html.Contains("href=\"https://web.test/account/bookings\"")));
    }
}
