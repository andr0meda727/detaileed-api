using Marketplace.Application.Listings;
using Marketplace.Application.Listings.GetMyListing;
using Marketplace.Application.Listings.PublishListing;
using Marketplace.Application.Listings.SaveListing;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Authorization;
using Shared.Api.Errors;

namespace Marketplace.Api.Controllers;

/// <summary>
/// The current studio's public profile on the customer map. Staff can see it; only the owner changes it.
/// </summary>
[ApiController]
[Route("api/marketplace/listing")]
[Authorize]
public class ListingController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ListingDto>> Get(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyListingQuery(), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// Creates the profile (unpublished, with the studio's current services) or updates it.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<ListingDto>> Save(
        ListingRequest request, CancellationToken cancellationToken)
    {
        return await sender.Send(new SaveListingCommand(request), cancellationToken);
    }

    [HttpPost("publish")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<IActionResult> Publish(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PublishListingCommand(Publish: true), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }

    [HttpPost("unpublish")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<IActionResult> Unpublish(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PublishListingCommand(Publish: false), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
