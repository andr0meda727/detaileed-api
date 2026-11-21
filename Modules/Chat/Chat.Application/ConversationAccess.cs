using Chat.Application.Abstractions;
using Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Chat.Application;

/// <summary>
/// Which conversations the caller may use from one side. The studio side goes through the tenant filter (the current
/// studio's inbox, nothing without one); the customer side has no tenant, so it matches the signed-in account itself.
/// Someone else's conversation is simply not there, so it answers 404 like one that doesn't exist.
/// </summary>
internal static class ConversationAccess
{
    // Retries after another request changed the same conversation; more than this means something else is wrong
    public const int MaxSaveAttempts = 3;

    public static Guid RequireUser(this ITenantContext tenant) =>
        tenant.UserId ?? throw new InvalidOperationException("The chat needs a signed-in user.");

    public static IQueryable<Conversation> Visible(IChatDbContext db, ITenantContext tenant, ChatSide side)
    {
        if (side == ChatSide.Studio) return db.Conversations;

        var userId = tenant.RequireUser();
        return db.ConversationsOfAnyStudio.Where(c => c.CustomerUserId == userId);
    }

    public static Task<Conversation?> FindAsync(
        IChatDbContext db, ITenantContext tenant, ChatSide side, Guid conversationId, CancellationToken cancellationToken) =>
        Visible(db, tenant, side).FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    /// <summary>
    /// The other side's messages in <paramref name="conversations"/> that <paramref name="side"/> hasn't read yet.
    /// </summary>
    public static IQueryable<Message> Unread(IChatDbContext db, IQueryable<Conversation> conversations, ChatSide side) =>
        side == ChatSide.Customer
            ? from c in conversations
              join m in db.MessagesOfAnyStudio on c.Id equals m.ConversationId
              where m.Side == ChatSide.Studio && (c.CustomerReadUpTo == null || m.SentAt > c.CustomerReadUpTo)
              select m
            : from c in conversations
              join m in db.MessagesOfAnyStudio on c.Id equals m.ConversationId
              where m.Side == ChatSide.Customer && (c.StudioReadUpTo == null || m.SentAt > c.StudioReadUpTo)
              select m;

    public static async Task<Dictionary<Guid, int>> UnreadCountsAsync(
        IChatDbContext db, IQueryable<Conversation> conversations, ChatSide side, CancellationToken cancellationToken) =>
        await Unread(db, conversations, side)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

    public static async Task<ConversationDto> ToDtoAsync(
        IChatDbContext db, Conversation conversation, ChatSide side, CancellationToken cancellationToken)
    {
        var unread = await Unread(db, db.ConversationsOfAnyStudio.Where(c => c.Id == conversation.Id), side)
            .CountAsync(cancellationToken);
        return ConversationDto.From(conversation, side, unread);
    }
}
