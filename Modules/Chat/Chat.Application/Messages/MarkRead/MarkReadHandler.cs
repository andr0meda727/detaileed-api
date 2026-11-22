using Chat.Application.Abstractions;
using Chat.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Messages.MarkRead;

public sealed record MarkReadCommand(ChatSide Side, Guid ConversationId, MarkReadRequest Request) : ICommand<Result>;

public sealed class MarkReadHandler(IChatDbContext db, ITenantContext tenant, IChatNotifier notifier)
    : ICommandHandler<MarkReadCommand, Result>
{
    /// <summary>
    /// One side has read the conversation up to a message it has seen. The time comes from that stored message, not
    /// from the app's clock, so nothing sent later can count as read. Read state only moves forward; when it moves,
    /// the other side gets the read receipt live.
    /// </summary>
    public async ValueTask<Result> Handle(MarkReadCommand command, CancellationToken cancellationToken)
    {
        var (side, conversationId, request) = command;

        for (var attempt = 0; attempt < ConversationAccess.MaxSaveAttempts; attempt++)
        {
            var conversation = await ConversationAccess.FindAsync(db, tenant, side, conversationId, cancellationToken);
            if (conversation == null) return ChatErrors.ConversationNotFound;

            var sentAt = await db.MessagesOfAnyStudio
                .Where(m => m.Id == request.UpToMessageId && m.ConversationId == conversation.Id)
                .Select(m => (DateTime?)m.SentAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (sentAt == null) return ChatErrors.MessageNotFound;

            if (!conversation.MarkRead(side, sentAt.Value)) return Result.Ok();

            // A message arrived or the other side read at the same moment: read the conversation again
            if (await db.TrySaveChangesAsync(cancellationToken) != SaveOutcome.Saved) continue;

            await notifier.ConversationReadAsync(conversation.StudioId, conversation.CustomerUserId,
                new ConversationReadDto(conversation.Id, side, sentAt.Value), cancellationToken);
            return Result.Ok();
        }

        throw new InvalidOperationException($"Conversation {conversationId} kept changing while it was being marked as read.");
    }
}
