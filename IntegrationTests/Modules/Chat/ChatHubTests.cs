using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chat.Api.Realtime;
using Identity.Domain.Studios;
using Identity.Domain.Users;
using IntegrationTests.Api;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Tenancy;

namespace IntegrationTests.Modules.Chat;

/// <summary>
/// The live side: changes saved over REST reach the other side's open connections. Studio A is published.
/// </summary>
[TestFixture]
public class ChatHubTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

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

    // Long polling over the in-memory test server; the test sign-in header rides on every request
    private async Task<HubConnection> ConnectAsync(Guid? userId)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, ChatHub.Path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                if (userId != null) options.Headers[TestAuthHandler.UserIdHeader] = userId.Value.ToString();
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    private static Task<JsonElement> NextAsync(HubConnection connection, string method)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(method, payload => received.TrySetResult(payload));
        return received.Task.WaitAsync(Deadline);
    }

    private async Task<(HttpClient Client, Guid UserId)> NewCustomerAsync()
    {
        var userId = Guid.NewGuid();
        await using (var db = PostgresFixture.CreateIdentityDbContext(new TenantContext()))
        {
            db.Users.Add(new User(userId, $"{userId:N}@test.pl", null, null));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClientFor(userId);
        (await client.PutAsJsonAsync("/api/me/profile", new { name = "Anna Nowak", phone = "500600700" })).EnsureSuccessStatusCode();
        return (client, userId);
    }

    private async Task<Guid> OpenAsync(HttpClient customer)
    {
        var response = await customer.PostAsJsonAsync("/api/me/conversations", new { studioId = _seed.StudioA.StudioId });
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Test]
    public async Task ACustomersMessage_ReachesStaffWatchingTheStudio_AndTheCustomersOtherTabs()
    {
        var (customer, customerId) = await NewCustomerAsync();
        var conversationId = await OpenAsync(customer);
        await using var staff = await ConnectAsync(_seed.StudioA.OwnerUserId);
        await staff.InvokeAsync(nameof(ChatHub.WatchStudio), _seed.StudioA.StudioId);
        await using var otherTab = await ConnectAsync(customerId);
        var atStaff = NextAsync(staff, ChatHub.MessageReceived);
        var atOtherTab = NextAsync(otherTab, ChatHub.MessageReceived);

        var sent = await customer.PostAsJsonAsync($"/api/me/conversations/{conversationId}/messages",
            new { body = "Dzień dobry!", clientMessageId = Guid.NewGuid() });
        sent.EnsureSuccessStatusCode();

        var message = await atStaff;
        Assert.Multiple(() =>
        {
            Assert.That(message.GetProperty("conversationId").GetGuid(), Is.EqualTo(conversationId));
            Assert.That(message.GetProperty("body").GetString(), Is.EqualTo("Dzień dobry!"));
            Assert.That(message.GetProperty("side").GetString(), Is.EqualTo("Customer"));
        });
        Assert.That((await atOtherTab).GetProperty("body").GetString(), Is.EqualTo("Dzień dobry!"));
    }

    [Test]
    public async Task TheStudioReadingIt_ReachesTheCustomerAsAReadReceipt()
    {
        var (customer, customerId) = await NewCustomerAsync();
        var conversationId = await OpenAsync(customer);
        var sent = await (await customer.PostAsJsonAsync($"/api/me/conversations/{conversationId}/messages",
            new { body = "Hej", clientMessageId = Guid.NewGuid() })).Content.ReadFromJsonAsync<JsonElement>();
        await using var customerConnection = await ConnectAsync(customerId);
        var receipt = NextAsync(customerConnection, ChatHub.ConversationRead);

        using var owner = _factory.CreateClientFor(_seed.StudioA.OwnerUserId);
        (await owner.PostAsJsonAsync($"/api/conversations/{conversationId}/read",
            new { upToMessageId = sent.GetProperty("id").GetGuid() })).EnsureSuccessStatusCode();

        var read = await receipt;
        Assert.That(read.GetProperty("side").GetString(), Is.EqualTo("Studio"));
        Assert.That(read.GetProperty("upTo").GetDateTime(), Is.EqualTo(sent.GetProperty("sentAt").GetDateTime()));
    }

    [Test]
    public async Task WatchingAStudioYouDontWorkAt_IsRefused()
    {
        await using var connection = await ConnectAsync(_seed.StudioA.OwnerUserId);

        Assert.That(() => connection.InvokeAsync(nameof(ChatHub.WatchStudio), _seed.StudioB.StudioId),
            Throws.InstanceOf<HubException>());
    }

    // Tests sign in with a header instead of a JWT, so the real JWT options are checked on their own
    [TestCase("/hubs/chat/negotiate", "token-from-query")]
    [TestCase("/hubs/chat", "token-from-query")]
    [TestCase("/api/conversations", null)]
    [TestCase("/hubsx/chat", null)]
    public async Task TheAccessTokenIsTakenFromTheQuery_OnlyForHubs(string path, string? expected)
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.QueryString = new QueryString("?access_token=token-from-query");
        var context = new MessageReceivedContext(
            http, new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), options);

        await options.Events.MessageReceived(context);

        Assert.That(context.Token, Is.EqualTo(expected));
    }

    [Test]
    public void AnAnonymousConnection_IsRefused()
    {
        var error = Assert.ThrowsAsync<HttpRequestException>(() => ConnectAsync(userId: null));

        Assert.That(error!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task StaffOfSeveralStudios_ConnectWithoutPickingOne_AndWatchEach()
    {
        var employee = Guid.NewGuid();
        await using (var db = PostgresFixture.CreateIdentityDbContext(new TenantContext()))
        {
            db.Users.Add(new User(employee, $"{employee:N}@test.pl", "Ewa", "Pracownik"));
            foreach (var studioId in new[] { _seed.StudioA.StudioId, _seed.StudioB.StudioId })
            {
                var studio = await db.Studios.Include(s => s.Members).SingleAsync(s => s.Id == studioId);
                studio.Admit(employee, Roles.Employee);
            }
            await db.SaveChangesAsync();
        }

        // REST needs the studio header for them; the hub doesn't
        using var rest = _factory.CreateClientFor(employee);
        var inbox = await rest.GetAsync("/api/conversations");
        await using var connection = await ConnectAsync(employee);

        Assert.That(inbox.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
        await connection.InvokeAsync(nameof(ChatHub.WatchStudio), _seed.StudioA.StudioId);
        await connection.InvokeAsync(nameof(ChatHub.WatchStudio), _seed.StudioB.StudioId);
    }
}
