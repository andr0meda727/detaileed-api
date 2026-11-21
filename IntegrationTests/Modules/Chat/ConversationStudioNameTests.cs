using System.Net.Http.Json;
using System.Text.Json;
using Chat.Domain;
using Chat.Infrastructure.Consumers;
using Identity.Contracts;
using Identity.Domain.Users;
using IntegrationTests.Api;
using IntegrationTests.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shared.Infrastructure.Tenancy;

namespace IntegrationTests.Modules.Chat;

/// <summary>
/// A conversation's copy of the studio's name follows Identity through the outbox and the bus.
/// </summary>
[TestFixture]
public class ConversationStudioNameTests
{
    private static readonly DateTime Opened = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task RenamingTheStudio_RenamesItInTheCustomersConversation()
    {
        var seed = new TwoStudiosSeed();
        await seed.SeedAsync();
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClientFor(seed.StudioA.OwnerUserId);
        await owner.PublishListingAsync(name: "Detail Studio");

        var customerId = Guid.NewGuid();
        await using (var identity = PostgresFixture.CreateIdentityDbContext(new TenantContext()))
        {
            identity.Users.Add(new User(customerId, $"{customerId:N}@test.pl", null, null));
            await identity.SaveChangesAsync();
        }
        using var customer = factory.CreateClientFor(customerId);
        (await customer.PutAsJsonAsync("/api/me/profile", new { name = "Anna", phone = "500600700" })).EnsureSuccessStatusCode();
        var opened = await customer.PostAsJsonAsync("/api/me/conversations", new { studioId = seed.StudioA.StudioId });
        var id = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await customer.PostAsJsonAsync($"/api/me/conversations/{id}/messages", new { body = "Hej", clientMessageId = Guid.NewGuid() }))
            .EnsureSuccessStatusCode();

        await owner.SetStudioDetailsAsync("Shine Gdańsk");

        string? name = null;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var mine = await customer.GetFromJsonAsync<JsonElement>("/api/me/conversations");
            name = mine.GetProperty("items").EnumerateArray()
                .Single(c => c.GetProperty("id").GetGuid() == id).GetProperty("studioName").GetString();
            if (name == "Shine Gdańsk") break;
            await Task.Delay(100);
        }

        Assert.That(name, Is.EqualTo("Shine Gdańsk"));
    }

    [Test]
    public async Task OnlyThatStudiosConversations_AreRenamed()
    {
        var studio = Guid.NewGuid();
        var other = await AddConversationAsync(Guid.NewGuid(), "Inne Studio");
        var mine = await AddConversationAsync(studio, "Studio");

        await ConsumeAsync(new StudioDetailsChanged(studio, Details("Shine"), Opened.AddMinutes(1)));

        Assert.That((await LoadAsync(mine)).StudioName, Is.EqualTo("Shine"));
        Assert.That((await LoadAsync(other)).StudioName, Is.EqualTo("Inne Studio"));
    }

    [Test]
    public async Task AnOlderSnapshot_DoesNotOverwriteANewerOne_AndARedeliveryChangesNothing()
    {
        var studio = Guid.NewGuid();
        var id = await AddConversationAsync(studio, "Studio");
        var newer = new StudioDetailsChanged(studio, Details("Nowa nazwa"), Opened.AddMinutes(10));

        await ConsumeAsync(newer);
        await ConsumeAsync(new StudioDetailsChanged(studio, Details("Stara nazwa"), Opened.AddMinutes(5)));
        await ConsumeAsync(newer);

        var conversation = await LoadAsync(id);
        Assert.That(conversation.StudioName, Is.EqualTo("Nowa nazwa"));
        Assert.That(conversation.StudioNameAsOf, Is.EqualTo(Opened.AddMinutes(10)));
    }

    [Test]
    public async Task ASnapshotFromBeforeTheConversation_IsIgnored()
    {
        var studio = Guid.NewGuid();
        var id = await AddConversationAsync(studio, "Aktualna nazwa");

        await ConsumeAsync(new StudioDetailsChanged(studio, Details("Dawna nazwa"), Opened.AddDays(-1)));

        Assert.That((await LoadAsync(id)).StudioName, Is.EqualTo("Aktualna nazwa"));
    }

    private static StudioDetails Details(string name) => new(name, null, null, null, null, null);

    private static async Task<Guid> AddConversationAsync(Guid studioId, string studioName)
    {
        var conversation = Conversation.Open(studioId, studioName, Guid.NewGuid(), "Anna", Opened);
        await using var db = PostgresFixture.CreateChatDbContext(new TenantContext());
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation.Id;
    }

    private static async Task<Conversation> LoadAsync(Guid id)
    {
        await using var db = PostgresFixture.CreateChatDbContext(new TenantContext());
        return await db.ConversationsOfAnyStudio.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private static async Task ConsumeAsync(StudioDetailsChanged message)
    {
        await using var db = PostgresFixture.CreateChatDbContext(new TenantContext());
        var context = Substitute.For<ConsumeContext<StudioDetailsChanged>>();
        context.Message.Returns(message);

        await new ConversationStudioNameConsumer(db, new FakeTimeProvider(Opened.AddHours(1))).Consume(context);
    }
}
