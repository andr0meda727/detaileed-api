using Chat.Application.Abstractions;
using Chat.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Pagination;
using Shared.Kernel.Pagination;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Messages.GetMessages;

public sealed record GetMessagesQuery(ChatSide Side, Guid ConversationId, CursorRequest Paging)
    : IQuery<Result<CursorResponse<MessageDto>>>;

public sealed class GetMessagesHandler(IChatDbContext db, ITenantContext tenant)
    : IQueryHandler<GetMessagesQuery, Result<CursorResponse<MessageDto>>>
{
    /// <summary>
    /// A conversation's messages, newest first, by cursor: new messages arrive at the top, so offset pages would
    /// repeat them. An app that was offline catches up by reading from the top until it reaches a message it has.
    /// </summary>
    public async ValueTask<Result<CursorResponse<MessageDto>>> Handle(GetMessagesQuery query, CancellationToken cancellationToken)
    {
        var (side, conversationId, paging) = query;
        TimeCursor? after = null;
        if (paging.Cursor != null)
        {
            if (!TimeCursor.TryDecode(paging.Cursor, out var cursor)) return TimeCursor.Invalid;
            after = cursor;
        }

        if (!await ConversationAccess.Visible(db, tenant, side).AnyAsync(c => c.Id == conversationId, cancellationToken))
            return ChatErrors.ConversationNotFound;

        var messages = db.MessagesOfAnyStudio.AsNoTracking().Where(m => m.ConversationId == conversationId);

        // Rows strictly after the cursor in (SentAt desc, Id desc) order; the id breaks ties between messages sent in
        // the same instant. Both are compared in Postgres, the same way it orders them.
        if (after is { At: var at, Id: var id })
            messages = messages.Where(m => m.SentAt < at || (m.SentAt == at && m.Id.CompareTo(id) < 0));

        var slice = await messages
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id)
            .ToCursorResponseAsync(paging.Limit, m => new TimeCursor(m.SentAt, m.Id).Encode(), cancellationToken);

        return slice.Map(MessageDto.From);
    }
}
