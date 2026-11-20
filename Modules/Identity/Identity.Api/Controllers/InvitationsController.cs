using Identity.Application;
using Identity.Application.Invitations.AcceptInvitation;
using Identity.Application.Invitations.DeclineInvitation;
using Identity.Application.Invitations.GetInvitation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;
using Shared.Api.Tenancy;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Identity.Api.Controllers;

/// <summary>
/// Answering an invitation happens before the person works in the studio, so no studio is required here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowWithoutStudio]
public class InvitationsController(ISender sender) : ControllerBase
{
    // Public: the link in the email opens this before the person signs in
    [HttpGet("{token:guid}")]
    public async Task<ActionResult<InvitationResponseDto>> GetInvitation(
        Guid token, CancellationToken cancellationToken)
    {
        var invitation = await sender.Send(new GetInvitationQuery(token), cancellationToken);

        return invitation != null
            ? invitation
            : this.ToProblem(Error.NotFound("INVITATION_NOT_FOUND", "Zaproszenie nie istnieje lub wygasło."));
    }

    [HttpPost("{token:guid}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(
        Guid token, [FromServices] ITenantContext tenant, CancellationToken cancellationToken)
    {
        if (tenant.UserId is not { } userId) return Unauthorized();

        var result = await sender.Send(new AcceptInvitationCommand(token, userId), cancellationToken);

        return result.Success
            ? Ok(new { message = "Zaproszenie zaakceptowane. Witaj w studiu!" })
            : this.ToProblem(result.Error!);
    }

    [HttpPost("{token:guid}/decline")]
    [Authorize]
    public async Task<IActionResult> Decline(
        Guid token, [FromServices] ITenantContext tenant, CancellationToken cancellationToken)
    {
        if (tenant.UserId is not { } userId) return Unauthorized();

        var result = await sender.Send(new DeclineInvitationCommand(token, userId), cancellationToken);

        return result.Success
            ? Ok(new { message = "Zaproszenie zostało odrzucone." })
            : this.ToProblem(result.Error!);
    }
}
