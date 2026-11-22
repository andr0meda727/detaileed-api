using Chat.Domain;
using FluentValidation;

namespace Chat.Application;

/// <summary>
/// A conversation as one side sees it. <see cref="UnreadCount"/> counts the other side's messages this side hasn't
/// read; <see cref="OtherSideReadUpTo"/> is the read receipt: the other side has read everything sent until then.
/// </summary>
public sealed record ConversationDto(
    Guid Id, Guid StudioId, string StudioName, string CustomerName, DateTime? LastMessageAt, string? LastMessagePreview,
    int UnreadCount, DateTime? OtherSideReadUpTo)
{
    public static ConversationDto From(Conversation conversation, ChatSide side, int unreadCount) =>
        new(conversation.Id, conversation.StudioId, conversation.StudioName, conversation.CustomerName,
            conversation.LastMessageAt, conversation.LastMessagePreview, unreadCount,
            conversation.ReadUpTo(side.Other()));
}

/// <summary>
/// <see cref="ClientMessageId"/> is the id the sender's app chose, so it can match the message it showed while
/// sending with the one that comes back (also over the live connection).
/// </summary>
public sealed record MessageDto(Guid Id, Guid ConversationId, ChatSide Side, string Body, DateTime SentAt, Guid ClientMessageId)
{
    public static MessageDto From(Message message) =>
        new(message.Id, message.ConversationId, message.Side, message.Body, message.SentAt, message.ClientMessageId);
}

/// <summary>
/// <see cref="Side"/> has read everything in the conversation sent up to <see cref="UpTo"/>.
/// </summary>
public sealed record ConversationReadDto(Guid ConversationId, ChatSide Side, DateTime UpTo);

public sealed record UnreadCountDto(int Count);

public sealed record OpenConversationRequest(Guid StudioId);

public sealed record OpenStudioConversationRequest(Guid BookingRequestId);

/// <summary>
/// <see cref="ClientMessageId"/> is a new id the app makes for each message and sends again when it retries, so a
/// message is never saved twice.
/// </summary>
public sealed record SendMessageRequest(string Body, Guid ClientMessageId);

/// <summary>
/// The newest message the reader has seen; everything up to it counts as read.
/// </summary>
public sealed record MarkReadRequest(Guid UpToMessageId);

public class OpenConversationRequestValidator : AbstractValidator<OpenConversationRequest>
{
    public OpenConversationRequestValidator()
    {
        RuleFor(x => x.StudioId).NotEmpty().WithMessage("Wybierz studio.");
    }
}

public class OpenStudioConversationRequestValidator : AbstractValidator<OpenStudioConversationRequest>
{
    public OpenStudioConversationRequestValidator()
    {
        RuleFor(x => x.BookingRequestId).NotEmpty().WithMessage("Wybierz prośbę o rezerwację.");
    }
}

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        // Measured as stored: the message is trimmed
        RuleFor(x => x.Body)
            .Must(body => !string.IsNullOrWhiteSpace(body)).WithMessage("Wiadomość nie może być pusta.")
            .Must(body => body == null || body.Trim().Length <= Conversation.MaxBodyLength)
            .WithMessage($"Wiadomość może mieć najwyżej {Conversation.MaxBodyLength} znaków.");

        RuleFor(x => x.ClientMessageId).NotEmpty().WithMessage("Brakuje identyfikatora wiadomości.");
    }
}

public class MarkReadRequestValidator : AbstractValidator<MarkReadRequest>
{
    public MarkReadRequestValidator()
    {
        RuleFor(x => x.UpToMessageId).NotEmpty().WithMessage("Wskaż ostatnią przeczytaną wiadomość.");
    }
}
