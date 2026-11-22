using Shared.Kernel.Tenancy;

namespace Chat.Domain;

/// <summary>
/// One message in a <see cref="Conversation"/>, created only by <see cref="Conversation.Post"/>. It belongs to the
/// conversation's studio (tenant data) whichever side wrote it. Messages are never edited.
/// </summary>
public class Message : ITenantEntity
{
    // For EF Core
    private Message() { }

    // Version 7, so ids roughly follow the order messages were written in
    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid StudioId { get; set; }

    public ChatSide Side { get; private set; }

    public Guid SenderUserId { get; private set; }

    public string Body { get; private set; } = null!;

    // Whole microseconds, as Postgres keeps them, so the value read back equals the one pushed live
    public DateTime SentAt { get; private set; }

    // Chosen by the sender's app; a retry with the same one returns this message instead of adding another
    public Guid ClientMessageId { get; private set; }

    internal static Message Create(
        Conversation conversation, ChatSide side, Guid senderUserId, string body, Guid clientMessageId, DateTime sentAt) =>
        new()
        {
            Id = Guid.CreateVersion7(sentAt),
            ConversationId = conversation.Id,
            StudioId = conversation.StudioId,
            Side = side,
            SenderUserId = senderUserId,
            Body = body,
            SentAt = sentAt,
            ClientMessageId = clientMessageId
        };

    /// <summary>
    /// Drops the sub-microsecond part of a UTC moment, which a Postgres <c>timestamptz</c> can't store.
    /// </summary>
    public static DateTime ToStoredPrecision(DateTime moment) =>
        new(moment.Ticks - moment.Ticks % 10, DateTimeKind.Utc);
}
