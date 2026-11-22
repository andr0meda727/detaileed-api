using Chat.Application.Abstractions;
using Chat.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Conversations.GetUnreadCount;

public sealed record GetUnreadCountQuery(ChatSide Side) : IQuery<UnreadCountDto>;

public sealed class GetUnreadCountHandler(IChatDbContext db, ITenantContext tenant)
    : IQueryHandler<GetUnreadCountQuery, UnreadCountDto>
{
    /// <summary>
    /// Unread messages across all of the caller's conversations from one side, for the badge in the navigation.
    /// </summary>
    public async ValueTask<UnreadCountDto> Handle(GetUnreadCountQuery query, CancellationToken cancellationToken)
    {
        var visible = ConversationAccess.Visible(db, tenant, query.Side);
        var count = await ConversationAccess.Unread(db, visible, query.Side).CountAsync(cancellationToken);
        return new UnreadCountDto(count);
    }
}
