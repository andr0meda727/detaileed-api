using Chat.Application.Abstractions;
using Chat.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Messages.SendMessage;

public sealed record SendMessageCommand(ChatSide Side, Guid ConversationId, SendMessageRequest Request)
    : ICommand<Result<MessageDto>>;

public sealed class SendMessageHandler(IChatDbContext db, ITenantContext tenant, IChatNotifier notifier, TimeProvider clock)
    : ICommandHandler<SendMessageCommand, Result<MessageDto>>
{
    /// <summary>
    /// Writes a message from one side and pushes it to both. Sending again with the same client message id returns
    /// the message already saved (and pushes nothing), so an app can retry after a dropped connection.
    /// </summary>
    public async ValueTask<Result<MessageDto>> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        var (side, conversationId, request) = command;
        var userId = tenant.RequireUser();

        for (var attempt = 0; attempt < ConversationAccess.MaxSaveAttempts; attempt++)
        {
            var conversation = await ConversationAccess.FindAsync(db, tenant, side, conversationId, cancellationToken);
            if (conversation == null) return ChatErrors.ConversationNotFound;

            var sent = await db.MessagesOfAnyStudio.AsNoTracking().FirstOrDefaultAsync(
                m => m.ConversationId == conversation.Id && m.SenderUserId == userId && m.ClientMessageId == request.ClientMessageId,
                cancellationToken);
            if (sent != null) return MessageDto.From(sent);

            var posted = conversation.Post(side, userId, request.Body, request.ClientMessageId, clock.GetUtcNow().UtcDateTime);
            if (!posted.Success) return posted.Error!;

            db.Messages.Add(posted.Value!);

            // Not saved: the same message was saved by a retry racing this one, or someone else changed the
            // conversation since it was read. Either way, read it again and decide anew.
            if (await db.TrySaveChangesAsync(cancellationToken) != SaveOutcome.Saved) continue;

            var message = MessageDto.From(posted.Value!);
            await notifier.MessageSentAsync(conversation.StudioId, conversation.CustomerUserId, message, cancellationToken);
            return message;
        }

        throw new InvalidOperationException($"Conversation {conversationId} kept changing while a message was being sent.");
    }
}
