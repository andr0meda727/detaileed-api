using Marketplace.Application.Reviews;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Api.Errors;
using Shared.Api.RateLimiting;
using Shared.Api.Tenancy;

namespace Marketplace.Api.Controllers;

/// <summary>
/// The signed-in customer's reviews of their completed visits. Needs no studio.
/// </summary>
[ApiController]
[Route("api/me/reviews")]
[Authorize]
[AllowWithoutStudio]
public class MyReviewsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Completed visits the customer can still review (up to 90 days after the visit).
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<ReviewableVisitDto>>> Pending(
        CancellationToken cancellationToken)
    {
        return await sender.Send(new GetReviewableVisitsQuery(), cancellationToken);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimits.Public)]
    public async Task<ActionResult<ReviewDto>> Submit(
        SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitReviewCommand(request), cancellationToken);

        return result.Success
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToProblem(result.Error!);
    }
}
