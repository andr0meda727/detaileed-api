using Inventory.Application.Consumption;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Authorization;
using Shared.Api.Errors;

namespace Inventory.Api.Controllers;

/// <summary>
/// What services use, what visits used, and what the stock is heading for. Quantities are in each item's unit.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
public class ConsumptionController(ISender sender) : ControllerBase
{
    /// <summary>
    /// A service's materials for a Medium car (empty when none are set).
    /// </summary>
    [HttpGet("recipes/{serviceId:guid}")]
    public async Task<ActionResult<RecipeDto>> GetRecipe(Guid serviceId, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetRecipeQuery(serviceId), cancellationToken);
    }

    [HttpPut("recipes/{serviceId:guid}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<RecipeDto>> SetRecipe(
        Guid serviceId, RecipeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SetRecipeCommand(serviceId, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// How much more material than a Medium car each size takes.
    /// </summary>
    [HttpGet("size-factors")]
    public async Task<ActionResult<SizeFactorsDto>> GetSizeFactors(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetSizeFactorsQuery(), cancellationToken);
    }

    [HttpPut("size-factors")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<SizeFactorsDto>> SetSizeFactors(
        SizeFactorsDto request, CancellationToken cancellationToken)
    {
        return await sender.Send(new SetSizeFactorsCommand(request), cancellationToken);
    }

    /// <summary>
    /// What a completed visit used: the norm for the car, the estimate taken off stock, and the actual amount if given.
    /// </summary>
    [HttpGet("consumptions/{bookingId:guid}")]
    public async Task<ActionResult<ConsumptionDto>> GetConsumption(Guid bookingId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetConsumptionQuery(bookingId), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// Records what the visit really used; stock moves by the difference and the estimates learn from it.
    /// </summary>
    [HttpPut("consumptions/{bookingId:guid}")]
    public async Task<ActionResult<ConsumptionDto>> CorrectConsumption(
        Guid bookingId, CorrectConsumptionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CorrectConsumptionCommand(bookingId, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// How accurate the estimates are per service and material, from the visits staff corrected.
    /// </summary>
    [HttpGet("consumptions/accuracy")]
    public async Task<ActionResult<List<AccuracyDto>>> GetAccuracy(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetAccuracyQuery(), cancellationToken);
    }

    /// <summary>
    /// For each material: recent daily use, what the booked visits of the next two weeks need, and when it runs out.
    /// </summary>
    [HttpGet("forecast")]
    public async Task<ActionResult<List<ForecastDto>>> GetForecast(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetForecastQuery(), cancellationToken);
    }
}
