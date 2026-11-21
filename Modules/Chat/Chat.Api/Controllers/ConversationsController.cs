using Chat.Application;
using Chat.Application.Conversations.GetConversations;
using Chat.Application.Conversations.GetUnreadCount;
using Chat.Application.Conversations.OpenAsStudio;
using Chat.Application.Messages.GetMessages;
using Chat.Application.Messages.MarkRead;
using Chat.Application.Messages.SendMessage;
using Chat.Domain;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Api.Errors;
using Shared.Api.RateLimiting;
using Shared.Kernel.Pagination;

namespace Chat.Api.Controllers;

/// <summary>
/// The current studio's conversations with customers: a shared inbox every staff member reads and answers.
/// </summary>
[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// The conversation with the customer who sent this booking request, started if there is none yet.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConversationDto>> Open(
        OpenStudioConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new OpenStudioConversationCommand(request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ConversationDto>>> GetAll(
        [FromQuery] PageRequest paging, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetConversationsQuery(ChatSide.Studio, paging), cancellationToken);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetUnreadCountQuery(ChatSide.Studio), cancellationToken);
    }

    /// <summary>
    /// Newest first; pass <c>nextCursor</c> back as <c>cursor</c> for older messages.
    /// </summary>
    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<CursorResponse<MessageDto>>> GetMessages(
        Guid id, [FromQuery] CursorRequest paging, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMessagesQuery(ChatSide.Studio, id, paging), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost("{id:guid}/messages")]
    [EnableRateLimiting(RateLimits.Chat)]
    public async Task<ActionResult<MessageDto>> Send(
        Guid id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendMessageCommand(ChatSide.Studio, id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, MarkReadRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkReadCommand(ChatSide.Studio, id, request), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
