using Identity.Application;
using Identity.Application.Studios.ChangeStudioDetails;
using Identity.Application.Studios.ChangeStudioTimeZone;
using Identity.Application.Studios.CreateStudio;
using Identity.Application.Studios.GetMyStudios;
using Identity.Application.Studios.GetStudioDetails;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;
using Shared.Api.Tenancy;
using Shared.Kernel.Tenancy;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudiosController(ISender sender) : ControllerBase
{
    // Used to choose a studio, so it can't require one
    [HttpGet("my")]
    [AllowWithoutStudio]
    public async Task<ActionResult<List<UserStudioDto>>> GetMyStudios(
        [FromServices] ITenantContext tenant, CancellationToken cancellationToken)
    {
        if (tenant.UserId is not { } userId) return Unauthorized();

        return await sender.Send(new GetMyStudiosQuery(userId), cancellationToken);
    }

    /// <summary>
    /// Opens a new studio with the caller as its owner. Works without a studio (onboarding right after sign-up) and
    /// for owners adding another location.
    /// </summary>
    [HttpPost]
    [AllowWithoutStudio]
    public async Task<ActionResult<UserStudioDto>> Create(
        CreateStudioRequest request, [FromServices] ITenantContext tenant, CancellationToken cancellationToken)
    {
        if (tenant.UserId is not { } userId) return Unauthorized();

        var result = await sender.Send(new CreateStudioCommand(userId, request), cancellationToken);

        return result.Success
            ? Created("/api/studios/my", result.Value)
            : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// The current studio's details: contact data, address and map pin, and its time zone.
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<StudioDetailsDto>> GetCurrent(CancellationToken cancellationToken) =>
        await sender.Send(new GetStudioDetailsQuery(), cancellationToken);

    /// <summary>
    /// Changes the current studio's details. The name, phone, address, city and pin are copied onto the studio's
    /// public listing a moment later.
    /// </summary>
    [HttpPut("current")]
    [Authorize(Policy = IdentityModule.OwnerOnlyPolicy)]
    public async Task<IActionResult> ChangeCurrent(ChangeStudioDetailsRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeStudioDetailsCommand(request), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// Sets the current studio's time zone (an IANA name such as "Europe/Warsaw"), used for reports and emails.
    /// </summary>
    [HttpPut("time-zone")]
    [Authorize(Policy = IdentityModule.OwnerOnlyPolicy)]
    public async Task<IActionResult> ChangeTimeZone(
        ChangeStudioTimeZoneRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeStudioTimeZoneCommand(request), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
