using Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Tenancy;

namespace IntegrationTests.Modules.Chat;

/// <summary>
/// How conversations are stored: the studio's inbox is tenant-filtered, a customer writes into a studio from outside
/// it, and racing writers are told apart from real failures.
/// </summary>
[TestFixture]
public class ChatDbContextTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static TenantContext Customer(Guid userId)
    {
        var tenant = new TenantContext();
        tenant.SetUserOnly(userId);
        return tenant;
    }

    private static TenantContext Staff(Guid studioId)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(studioId, Guid.NewGuid());
        return tenant;
    }

    [Test]
    public async Task ACustomersConversation_ShowsInThatStudiosInboxOnly()
    {
        var studio = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var conversation = Conversation.Open(studio, "Detail Studio", anna, "Anna", Now);
        await using (var db = PostgresFixture.CreateChatDbContext(Customer(anna)))
        {
            db.Conversations.Add(conversation);
            db.Messages.Add(conversation.Post(ChatSide.Customer, anna, "Dzień dobry", Guid.NewGuid(), Now).Value!);
            await db.SaveChangesAsync();
        }

        await using var asStudio = PostgresFixture.CreateChatDbContext(Staff(studio));
        await using var asOtherStudio = PostgresFixture.CreateChatDbContext(Staff(Guid.NewGuid()));
        await using var asCustomer = PostgresFixture.CreateChatDbContext(Customer(anna));

        Assert.That(await asStudio.Conversations.Select(c => c.Id).ToListAsync(), Is.EqualTo(new[] { conversation.Id }));
        Assert.That(await asStudio.Messages.CountAsync(m => m.ConversationId == conversation.Id), Is.EqualTo(1));
        Assert.That(await asOtherStudio.Conversations.AnyAsync(c => c.Id == conversation.Id), Is.False);
        // Without a tenant the filter shows nothing; the customer side goes through the named sets
        Assert.That(await asCustomer.Conversations.AnyAsync(), Is.False);
        Assert.That(await asCustomer.ConversationsOfAnyStudio.CountAsync(c => c.CustomerUserId == anna), Is.EqualTo(1));
    }

    [Test]
    public async Task StaffWritingAsACustomer_KeepTheStudioTheyWriteTo()
    {
        var theirOwnStudio = Guid.NewGuid();
        var otherStudio = Guid.NewGuid();
        var staffMember = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(theirOwnStudio, staffMember);

        var conversation = Conversation.Open(otherStudio, "Inne Studio", staffMember, "Jan", Now);
        await using (var db = PostgresFixture.CreateChatDbContext(tenant))
        {
            db.Conversations.Add(conversation);
            db.Messages.Add(conversation.Post(ChatSide.Customer, staffMember, "Hej", Guid.NewGuid(), Now).Value!);
            await db.SaveChangesAsync();
        }

        await using var read = PostgresFixture.CreateChatDbContext(new TenantContext());
        var stored = await read.ConversationsOfAnyStudio.SingleAsync(c => c.Id == conversation.Id);
        var message = await read.MessagesOfAnyStudio.SingleAsync(m => m.ConversationId == conversation.Id);
        Assert.That(stored.StudioId, Is.EqualTo(otherStudio));
        Assert.That(message.StudioId, Is.EqualTo(otherStudio));
    }

    [Test]
    public async Task OpeningTheSameConversationTwice_LosesTheRace_InsteadOfFailing()
    {
        var studio = Guid.NewGuid();
        var anna = Guid.NewGuid();
        await using (var first = PostgresFixture.CreateChatDbContext(Customer(anna)))
        {
            first.Conversations.Add(Conversation.Open(studio, "Detail Studio", anna, "Anna", Now));
            Assert.That(await first.TrySaveChangesAsync(), Is.True);
        }

        await using var second = PostgresFixture.CreateChatDbContext(Customer(anna));
        second.Conversations.Add(Conversation.Open(studio, "Detail Studio", anna, "Anna", Now));

        Assert.That(await second.TrySaveChangesAsync(), Is.False);
        Assert.That(second.ChangeTracker.Entries(), Is.Empty);
        Assert.That(await second.ConversationsOfAnyStudio.CountAsync(c => c.StudioId == studio && c.CustomerUserId == anna),
            Is.EqualTo(1));
    }

    [Test]
    public async Task ARetriedSend_LosesTheRace_InsteadOfAddingTheMessageAgain()
    {
        var studio = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();
        var conversation = Conversation.Open(studio, "Detail Studio", anna, "Anna", Now);
        await using (var db = PostgresFixture.CreateChatDbContext(Customer(anna)))
        {
            db.Conversations.Add(conversation);
            db.Messages.Add(conversation.Post(ChatSide.Customer, anna, "Hej", clientMessageId, Now).Value!);
            await db.SaveChangesAsync();
        }

        await using var retry = PostgresFixture.CreateChatDbContext(Customer(anna));
        var loaded = await retry.ConversationsOfAnyStudio.SingleAsync(c => c.Id == conversation.Id);
        retry.Messages.Add(loaded.Post(ChatSide.Customer, anna, "Hej", clientMessageId, Now.AddSeconds(1)).Value!);

        Assert.That(await retry.TrySaveChangesAsync(), Is.False);
        Assert.That(await retry.MessagesOfAnyStudio.CountAsync(m => m.ConversationId == conversation.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task TwoWritersChangingOneConversation_AreCaught()
    {
        var studio = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var conversation = Conversation.Open(studio, "Detail Studio", anna, "Anna", Now);
        await using (var db = PostgresFixture.CreateChatDbContext(Customer(anna)))
        {
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        await using var customer = PostgresFixture.CreateChatDbContext(Customer(anna));
        await using var staff = PostgresFixture.CreateChatDbContext(Staff(studio));
        var asCustomer = await customer.ConversationsOfAnyStudio.SingleAsync(c => c.Id == conversation.Id);
        var asStaff = await staff.Conversations.SingleAsync(c => c.Id == conversation.Id);

        customer.Messages.Add(asCustomer.Post(ChatSide.Customer, anna, "Hej", Guid.NewGuid(), Now).Value!);
        await customer.SaveChangesAsync();
        staff.Messages.Add(asStaff.Post(ChatSide.Studio, Guid.NewGuid(), "Dzień dobry", Guid.NewGuid(), Now).Value!);

        Assert.That(() => staff.SaveChangesAsync(), Throws.InstanceOf<DbUpdateConcurrencyException>());
    }
}
