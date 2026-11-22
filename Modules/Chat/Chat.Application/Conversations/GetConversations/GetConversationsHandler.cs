using Chat.Application.Abstractions;
using Chat.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Pagination;
using Shared.Kernel.Pagination;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Conversations.GetConversations;

public sealed record GetConversationsQuery(ChatSide Side, PageRequest Paging) : IQuery<PagedResponse<ConversationDto>>;

public sealed class GetConversationsHandler(IChatDbContext db, ITenantContext tenant)
    : IQueryHandler<GetConversationsQuery, PagedResponse<ConversationDto>>
{
    /// <summary>
    /// The caller's conversations from one side, latest message first. A conversation opened but never written to
    /// isn't listed: there is nothing in it yet.
    /// </summary>
    public async ValueTask<PagedResponse<ConversationDto>> Handle(GetConversationsQuery query, CancellationToken cancellationToken)
    {
        var (side, paging) = query;
        var visible = ConversationAccess.Visible(db, tenant, side);

        var page = await visible.AsNoTracking()
            .Where(c => c.LastMessageAt != null)
            .OrderByDescending(c => c.LastMessageAt).ThenByDescending(c => c.Id)
            .ToPagedResponseAsync(paging, cancellationToken);

        var ids = page.Items.Select(c => c.Id).ToList();
        var unread = await ConversationAccess.UnreadCountsAsync(db, visible.Where(c => ids.Contains(c.Id)), side, cancellationToken);

        return page.Map(c => ConversationDto.From(c, side, unread.GetValueOrDefault(c.Id)));
    }
}
