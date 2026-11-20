using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Application.Planning.Bays;
using Scheduling.Application.Planning.Bays.CreateBay;
using Scheduling.Application.Planning.Bays.DeactivateBay;
using Scheduling.Application.Planning.Bays.GetBays;
using Scheduling.Application.Planning.Bays.UpdateBay;
using Shared.Api.Authorization;
using Shared.Api.Errors;

namespace Scheduling.Api.Controllers;

/// <summary>
/// The studio's bays (stations where one car is worked on at a time). Any staff member reads them; the owner
/// changes them.
/// </summary>
[ApiController]
[Route("api/bays")]
[Authorize]
public class BaysController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BayDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return await sender.Send(new GetBaysQuery(), cancellationToken);
    }

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<BayDto>> Create(
        BayRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateBayCommand(request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<BayDto>> Rename(
        Guid id, BayRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateBayCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// Takes the bay out of planning; past bookings keep it.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<IActionResult> Deactivate(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateBayCommand(id), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
