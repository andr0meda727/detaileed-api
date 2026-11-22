using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Domain.Users;
using IntegrationTests.Api;
using IntegrationTests.Infrastructure;
using Scheduling.Domain.Requests;
using Shared.Infrastructure.Tenancy;
using Shared.Kernel.Vehicles;

namespace IntegrationTests.Modules.Chat;

/// <summary>
/// The chat over REST: a customer writes to a published studio (A), the studio's staff answer from a shared inbox.
/// Studio B is not published.
/// </summary>
[TestFixture]
public class ChatEndpointTests
{
    private ApiFactory _factory = null!;
    private TwoStudiosSeed _seed = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        _seed = new TwoStudiosSeed();
        await _seed.SeedAsync();
        _factory = new ApiFactory();

        using var owner = _factory.CreateClientFor(_seed.StudioA.OwnerUserId);
        await owner.PublishListingAsync();
    }

    [OneTimeTearDown]
    public async Task TearDownAsync() => await _factory.DisposeAsync();

    private HttpClient OwnerA() => _factory.CreateClientFor(_seed.StudioA.OwnerUserId);

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var code) ? code.GetString() : null;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// A signed-up account with no studio, optionally with contact details.
    /// </summary>
    private async Task<(HttpClient Client, Guid UserId)> NewCustomerAsync(bool withProfile = true, string name = "Anna Nowak")
    {
        var userId = Guid.NewGuid();
        await using (var db = PostgresFixture.CreateIdentityDbContext(new TenantContext()))
        {
            db.Users.Add(new User(userId, $"{userId:N}@test.pl", null, null));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClientFor(userId);
        if (withProfile)
            (await client.PutAsJsonAsync("/api/me/profile", new { name, phone = "500600700" })).EnsureSuccessStatusCode();

        return (client, userId);
    }

    private async Task<Guid> OpenAsync(HttpClient customer) =>
        (await JsonAsync(await customer.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioA.StudioId })))
        .GetProperty("id").GetGuid();

    private static async Task<JsonElement> SendAsync(HttpClient client, string path, string body, Guid? clientMessageId = null) =>
        await JsonAsync(await client.PostAsJsonAsync(path, new { body, clientMessageId = clientMessageId ?? Guid.NewGuid() }));

    private static JsonElement Find(JsonElement page, Guid conversationId) =>
        page.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == conversationId);

    private static IEnumerable<Guid> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid());

    [Test]
    public async Task Customer_OpensOneConversationPerStudio()
    {
        var (customer, _) = await NewCustomerAsync();

        var first = await JsonAsync(await customer.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioA.StudioId }));
        var again = await JsonAsync(await customer.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioA.StudioId }));

        Assert.Multiple(() =>
        {
            Assert.That(again.GetProperty("id").GetGuid(), Is.EqualTo(first.GetProperty("id").GetGuid()));
            Assert.That(first.GetProperty("studioName").GetString(), Is.EqualTo("Detail Studio"));
            Assert.That(first.GetProperty("customerName").GetString(), Is.EqualTo("Anna Nowak"));
            Assert.That(first.GetProperty("unreadCount").GetInt32(), Is.Zero);
        });
    }

    [Test]
    public async Task Customer_CannotOpen_WithAnUnpublishedStudio_WithoutAProfile_OrWithTheirOwnStudio()
    {
        var (customer, _) = await NewCustomerAsync();
        var (noProfile, _) = await NewCustomerAsync(withProfile: false);
        using var ownerA = OwnerA();
        (await ownerA.PutAsJsonAsync("/api/me/profile", new { name = "Jan Właściciel", phone = "500600701" })).EnsureSuccessStatusCode();

        var unpublished = await customer.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioB.StudioId });
        var withoutProfile = await noProfile.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioA.StudioId });
        var ownStudio = await ownerA.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioA.StudioId });

        Assert.That(await ProblemCodeAsync(unpublished), Is.EqualTo("CHAT_STUDIO_NOT_AVAILABLE"));
        Assert.That(await ProblemCodeAsync(withoutProfile), Is.EqualTo("CHAT_PROFILE_REQUIRED"));
        Assert.That(await ProblemCodeAsync(ownStudio), Is.EqualTo("CHAT_OWN_STUDIO"));
    }

    [Test]
    public async Task CustomerWrites_StudioSeesItUnread_ReadsIt_AndTheCustomerGetsTheReceipt()
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        using var owner = OwnerA();
        var unreadBefore = (await owner.GetFromJsonAsync<JsonElement>("/api/conversations/unread-count")).GetProperty("count").GetInt32();

        await SendAsync(customer, $"/api/me/conversations/{id}/messages", "Dzień dobry,\nczy macie wolny termin?");
        var last = await SendAsync(customer, $"/api/me/conversations/{id}/messages", "W sobotę?");

        var inbox = Find(await owner.GetFromJsonAsync<JsonElement>("/api/conversations?pageSize=100"), id);
        var unread = (await owner.GetFromJsonAsync<JsonElement>("/api/conversations/unread-count")).GetProperty("count").GetInt32();
        Assert.Multiple(() =>
        {
            Assert.That(inbox.GetProperty("customerName").GetString(), Is.EqualTo("Anna Nowak"));
            Assert.That(inbox.GetProperty("lastMessagePreview").GetString(), Is.EqualTo("W sobotę?"));
            Assert.That(inbox.GetProperty("unreadCount").GetInt32(), Is.EqualTo(2));
            Assert.That(unread, Is.EqualTo(unreadBefore + 2));
        });

        var read = await owner.PostAsJsonAsync($"/api/conversations/{id}/read", new { upToMessageId = last.GetProperty("id").GetGuid() });
        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var afterRead = Find(await owner.GetFromJsonAsync<JsonElement>("/api/conversations?pageSize=100"), id);
        var mine = Find(await customer.GetFromJsonAsync<JsonElement>("/api/me/conversations"), id);
        Assert.That(afterRead.GetProperty("unreadCount").GetInt32(), Is.Zero);
        Assert.That(mine.GetProperty("otherSideReadUpTo").GetDateTime(), Is.EqualTo(last.GetProperty("sentAt").GetDateTime()));
    }

    [Test]
    public async Task StudioAnswers_AndTheCustomerSeesBothSidesNewestFirst()
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        using var owner = OwnerA();

        await SendAsync(customer, $"/api/me/conversations/{id}/messages", "Pytanie");
        await SendAsync(owner, $"/api/conversations/{id}/messages", "Odpowiedź");

        var messages = await customer.GetFromJsonAsync<JsonElement>($"/api/me/conversations/{id}/messages");
        var items = messages.GetProperty("items").EnumerateArray().ToList();
        Assert.That(items.Select(m => m.GetProperty("body").GetString()), Is.EqualTo(new[] { "Odpowiedź", "Pytanie" }));
        Assert.That(items.Select(m => m.GetProperty("side").GetString()), Is.EqualTo(new[] { "Studio", "Customer" }));
        Assert.That((await customer.GetFromJsonAsync<JsonElement>("/api/me/conversations/unread-count")).GetProperty("count").GetInt32(),
            Is.EqualTo(1));
    }

    [Test]
    public async Task ARetriedSend_ReturnsTheSameMessage()
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        var clientMessageId = Guid.NewGuid();

        var first = await SendAsync(customer, $"/api/me/conversations/{id}/messages", "Hej", clientMessageId);
        var retry = await SendAsync(customer, $"/api/me/conversations/{id}/messages", "Hej", clientMessageId);

        var messages = await customer.GetFromJsonAsync<JsonElement>($"/api/me/conversations/{id}/messages");
        Assert.That(retry.GetProperty("id").GetGuid(), Is.EqualTo(first.GetProperty("id").GetGuid()));
        Assert.That(messages.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task Messages_PageOlderByCursor()
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        foreach (var body in new[] { "1", "2", "3" })
            await SendAsync(customer, $"/api/me/conversations/{id}/messages", body);

        var first = await customer.GetFromJsonAsync<JsonElement>($"/api/me/conversations/{id}/messages?limit=2");
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await customer.GetFromJsonAsync<JsonElement>($"/api/me/conversations/{id}/messages?limit=2&cursor={cursor}");
        var invalid = await customer.GetAsync($"/api/me/conversations/{id}/messages?cursor=nonsense");

        Assert.That(first.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("body").GetString()), Is.EqualTo(new[] { "3", "2" }));
        Assert.That(second.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("body").GetString()), Is.EqualTo(new[] { "1" }));
        Assert.That(second.GetProperty("nextCursor").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(await ProblemCodeAsync(invalid), Is.EqualTo("INVALID_CURSOR"));
    }

    [Test]
    public async Task AConversationWithoutMessages_IsNotListed()
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        using var owner = OwnerA();

        Assert.That(Ids(await customer.GetFromJsonAsync<JsonElement>("/api/me/conversations")), Does.Not.Contain(id));
        Assert.That(Ids(await owner.GetFromJsonAsync<JsonElement>("/api/conversations?pageSize=100")), Does.Not.Contain(id));
    }

    [Test]
    public async Task OtherStudiosAndOtherCustomers_CannotSeeOrWrite()
    {
        var (customer, _) = await NewCustomerAsync();
        var (stranger, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        var message = await SendAsync(customer, $"/api/me/conversations/{id}/messages", "Prywatne");
        using var ownerB = _factory.CreateClientFor(_seed.StudioB.OwnerUserId);

        // Each is a 404: someone else's conversation is simply not there
        Assert.That((await ownerB.GetAsync($"/api/conversations/{id}/messages")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await ownerB.PostAsJsonAsync($"/api/conversations/{id}/messages", new { body = "x", clientMessageId = Guid.NewGuid() }))
            .StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await stranger.GetAsync($"/api/me/conversations/{id}/messages")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await stranger.PostAsJsonAsync($"/api/me/conversations/{id}/read",
            new { upToMessageId = message.GetProperty("id").GetGuid() })).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(Ids(await ownerB.GetFromJsonAsync<JsonElement>("/api/conversations")), Does.Not.Contain(id));
    }

    [Test]
    public async Task MarkRead_NeedsAMessageOfThatConversation()
    {
        var (customer, _) = await NewCustomerAsync();
        var (other, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);
        var otherId = await OpenAsync(other);
        var othersMessage = await SendAsync(other, $"/api/me/conversations/{otherId}/messages", "Hej");
        using var owner = OwnerA();

        var response = await owner.PostAsJsonAsync($"/api/conversations/{id}/read",
            new { upToMessageId = othersMessage.GetProperty("id").GetGuid() });

        Assert.That(await ProblemCodeAsync(response), Is.EqualTo("MESSAGE_NOT_FOUND"));
    }

    [TestCase("   ")]
    [TestCase(null)]
    public async Task Send_RejectsAnEmptyMessage(string? body)
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);

        var response = await customer.PostAsJsonAsync($"/api/me/conversations/{id}/messages", new { body, clientMessageId = Guid.NewGuid() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Send_RejectsATooLongMessage()
    {
        var (customer, _) = await NewCustomerAsync();
        var id = await OpenAsync(customer);

        var response = await customer.PostAsJsonAsync($"/api/me/conversations/{id}/messages",
            new { body = new string('a', 2001), clientMessageId = Guid.NewGuid() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Studio_WritesFirst_OnlyToACustomerWhoSentARequest()
    {
        var (customer, userId) = await NewCustomerAsync(name: "Ewa Kowalska");
        var request = OnlineBookingRequest.Submit(
            _seed.StudioA.StudioId, "Detail Studio", new RequestCustomer(userId, "Ewa Kowalska", "500600700", "ewa@test.pl"),
            new RequestVehicle("KR 7", null, null, VehicleSize.Medium),
            [new RequestedService(_seed.StudioA.ServiceId, "Polishing", null)], DateTime.UtcNow.AddDays(2), null);
        await using (var db = PostgresFixture.CreateSchedulingDbContext(new TenantContext()))
        {
            db.BookingRequests.Add(request);
            await db.SaveChangesAsync();
        }
        using var owner = OwnerA();
        using var ownerB = _factory.CreateClientFor(_seed.StudioB.OwnerUserId);

        var opened = await JsonAsync(await owner.PostAsJsonAsync("/api/conversations", new { bookingRequestId = request.Id }));
        var id = opened.GetProperty("id").GetGuid();
        await SendAsync(owner, $"/api/conversations/{id}/messages", "Dzień dobry, mamy pytanie o auto.");
        var unknown = await owner.PostAsJsonAsync("/api/conversations", new { bookingRequestId = Guid.NewGuid() });
        var otherStudio = await ownerB.PostAsJsonAsync("/api/conversations", new { bookingRequestId = request.Id });

        Assert.That(opened.GetProperty("customerName").GetString(), Is.EqualTo("Ewa Kowalska"));
        Assert.That(Find(await customer.GetFromJsonAsync<JsonElement>("/api/me/conversations"), id)
            .GetProperty("unreadCount").GetInt32(), Is.EqualTo(1));
        // The customer opening a conversation with the studio finds the same one
        Assert.That(await OpenAsync(customer), Is.EqualTo(id));
        Assert.That(await ProblemCodeAsync(unknown), Is.EqualTo("CHAT_NO_CONTACT"));
        Assert.That(await ProblemCodeAsync(otherStudio), Is.EqualTo("CHAT_NO_CONTACT"));
    }

    [Test]
    public async Task Sending_IsLimitedPerUser_NotPerAddress()
    {
        await using var limited = new ApiFactory(new Dictionary<string, string?> { ["RateLimiting:ChatPermitLimit"] = "2" });
        var original = _factory;
        _factory = limited;
        try
        {
            // Test clients all come from the same (missing) address, so only the user tells them apart
            var (anna, _) = await NewCustomerAsync();
            var (ewa, _) = await NewCustomerAsync(name: "Ewa Kowalska");
            var annas = await OpenAsync(anna);
            var ewas = await OpenAsync(ewa);

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 3; i++)
            {
                var response = await anna.PostAsJsonAsync($"/api/me/conversations/{annas}/messages",
                    new { body = $"Wiadomość {i}", clientMessageId = Guid.NewGuid() });
                statuses.Add(response.StatusCode);
            }
            var ewaSends = await ewa.PostAsJsonAsync($"/api/me/conversations/{ewas}/messages",
                new { body = "Hej", clientMessageId = Guid.NewGuid() });

            Assert.That(statuses, Is.EqualTo(new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests }));
            Assert.That(ewaSends.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        finally
        {
            _factory = original;
        }
    }

    [Test]
    public async Task StaffOfAnotherStudio_CanWriteToStudioAAsACustomer()
    {
        // B's owner has one membership, so studio B is resolved as their tenant on every call
        using var ownerB = _factory.CreateClientFor(_seed.StudioB.OwnerUserId);
        (await ownerB.PutAsJsonAsync("/api/me/profile", new { name = "Jan z B", phone = "500600702" })).EnsureSuccessStatusCode();

        var id = await OpenAsync(ownerB);
        await SendAsync(ownerB, $"/api/me/conversations/{id}/messages", "Pytanie od sąsiada");

        using var ownerA = OwnerA();
        Assert.That(Ids(await ownerA.GetFromJsonAsync<JsonElement>("/api/conversations?pageSize=100")), Does.Contain(id));
        Assert.That(Ids(await ownerB.GetFromJsonAsync<JsonElement>("/api/conversations")), Does.Not.Contain(id));
    }
}
