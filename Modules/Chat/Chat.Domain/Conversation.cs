using Shared.Kernel.Auditing;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Chat.Domain;

/// <summary>
/// The one thread between a studio and a customer account. The studio answers as a shared inbox, so read state is
/// kept per side, not per person. It belongs to the studio (tenant data), but the customer opens and writes to it
/// from outside any tenant, so the studio is always set explicitly. Messages are stored separately and never loaded
/// through the conversation; it keeps only what the conversation list needs (the last message, read state).
/// </summary>
public class Conversation : ITenantEntity, IAuditableEntity
{
    public const int MaxBodyLength = 2000;
    public const int PreviewLength = 100;

    // For EF Core
    private Conversation() { }

    public Guid Id { get; private set; }

    public Guid StudioId { get; set; }

    public Guid CustomerUserId { get; private set; }

    // A copy of the studio's name, kept current from Identity's events
    public string StudioName { get; private set; } = null!;

    // When the studio's name was copied, so an older event never replaces a newer name
    public DateTime StudioNameAsOf { get; private set; }

    // A copy of the customer's name from when the conversation was opened
    public string CustomerName { get; private set; } = null!;

    public DateTime? LastMessageAt { get; private set; }

    public string? LastMessagePreview { get; private set; }

    // That side has read everything sent up to this moment
    public DateTime? CustomerReadUpTo { get; private set; }

    public DateTime? StudioReadUpTo { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public static Conversation Open(Guid studioId, string studioName, Guid customerUserId, string customerName, DateTime now)
    {
        if (studioId == Guid.Empty) throw new ArgumentException("A conversation is with a studio.", nameof(studioId));
        if (customerUserId == Guid.Empty) throw new ArgumentException("A conversation is with a customer.", nameof(customerUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(studioName);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerName);

        return new Conversation
        {
            Id = Guid.NewGuid(),
            StudioId = studioId,
            StudioName = studioName.Trim(),
            StudioNameAsOf = now,
            CustomerUserId = customerUserId,
            CustomerName = customerName.Trim()
        };
    }

    public DateTime? ReadUpTo(ChatSide side) => side == ChatSide.Customer ? CustomerReadUpTo : StudioReadUpTo;

    /// <summary>
    /// Writes a message from <paramref name="side"/>; the caller adds the returned message to the database. Your own
    /// message counts as read by your side. A message is never dated before the previous one, so the order stays
    /// right even if the server's clock steps back.
    /// </summary>
    public Result<Message> Post(ChatSide side, Guid senderUserId, string body, Guid clientMessageId, DateTime now)
    {
        if (senderUserId == Guid.Empty) throw new ArgumentException("A message has an author.", nameof(senderUserId));
        if (clientMessageId == Guid.Empty) throw new ArgumentException("A message needs the sender's id for it.", nameof(clientMessageId));

        var text = body.Trim();
        if (text.Length == 0) return ChatErrors.MessageEmpty;
        if (text.Length > MaxBodyLength) return ChatErrors.MessageTooLong;

        var sentAt = Message.ToStoredPrecision(now);
        if (LastMessageAt > sentAt) sentAt = LastMessageAt.Value;

        var message = Message.Create(this, side, senderUserId, text, clientMessageId, sentAt);
        LastMessageAt = sentAt;
        LastMessagePreview = Preview(text);
        MarkRead(side, sentAt);
        return message;
    }

    /// <summary>
    /// <paramref name="side"/> has read everything sent up to <paramref name="upTo"/>: the time of a stored message,
    /// never a client's clock. Read state only moves forward; returns whether it changed.
    /// </summary>
    public bool MarkRead(ChatSide side, DateTime upTo)
    {
        if (ReadUpTo(side) >= upTo) return false;

        if (side == ChatSide.Customer) CustomerReadUpTo = upTo;
        else StudioReadUpTo = upTo;
        return true;
    }

    /// <summary>
    /// The studio's name as of <paramref name="asOf"/>. A redelivered or older change is ignored; returns whether the
    /// name was replaced.
    /// </summary>
    public bool RenameStudio(string name, DateTime asOf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (asOf <= StudioNameAsOf) return false;

        StudioName = name.Trim();
        StudioNameAsOf = asOf;
        return true;
    }

    // One line for the conversation list: line breaks become spaces, and long text is cut with an ellipsis
    private static string Preview(string text)
    {
        var line = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return line.Length <= PreviewLength ? line : string.Concat(line.AsSpan(0, PreviewLength - 1).TrimEnd(), "…");
    }
}
