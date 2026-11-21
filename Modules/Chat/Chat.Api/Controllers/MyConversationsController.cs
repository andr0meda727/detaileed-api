using Chat.Application;
using Chat.Application.Conversations.GetConversations;
using Chat.Application.Conversations.GetUnreadCount;
using Chat.Application.Conversations.OpenAsCustomer;
using Chat.Application.Messages.GetMessages;
using Chat.Application.Messages.MarkRead;
using Chat.Application.Messages.SendMessage;
using Chat.Domain;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;
using Shared.Api.Tenancy;
using Shared.Kernel.Pagination;

namespace Chat.Api.Controllers;

/// <summary>
/// A signed-in customer's conversations with studios.
/// </summary>
[ApiController]
[Route("api/me/conversations")]
[Authorize]
[AllowWithoutStudio]
public class MyConversationsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// The conversation with a published studio, started if there is none yet.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConversationDto>> Open(OpenConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new OpenConversationCommand(request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ConversationDto>>> GetAll(
        [FromQuery] PageRequest paging, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetConversationsQuery(ChatSide.Customer, paging), cancellationToken);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetUnreadCountQuery(ChatSide.Customer), cancellationToken);
    }

    /// <summary>
    /// Newest first; pass <c>nextCursor</c> back as <c>cursor</c> for older messages.
    /// </summary>
    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<CursorResponse<MessageDto>>> GetMessages(
        Guid id, [FromQuery] CursorRequest paging, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMessagesQuery(ChatSide.Customer, id, paging), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageDto>> Send(
        Guid id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendMessageCommand(ChatSide.Customer, id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, MarkReadRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkReadCommand(ChatSide.Customer, id, request), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
