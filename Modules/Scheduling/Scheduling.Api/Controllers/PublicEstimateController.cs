using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scheduling.Application.Requests;
using Scheduling.Application.Requests.Estimate;
using Shared.Api.Errors;
using Shared.Api.RateLimiting;
using Shared.Api.Tenancy;

namespace Scheduling.Api.Controllers;

/// <summary>
/// The price preview on a studio's public page, without signing in.
/// </summary>
[ApiController]
[Route("api/public/studios/{studioId:guid}/estimate")]
[AllowAnonymous]
[AllowWithoutStudio]
[EnableRateLimiting(RateLimits.Public)]
public class PublicEstimateController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EstimateDto>> Estimate(
        Guid studioId, EstimateRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EstimateQuery(studioId, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }
}
