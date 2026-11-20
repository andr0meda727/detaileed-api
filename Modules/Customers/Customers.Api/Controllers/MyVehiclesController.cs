using Customers.Application;
using Customers.Application.Garage.AddVehicle;
using Customers.Application.Garage.DeleteVehicle;
using Customers.Application.Garage.GetMyVehicles;
using Customers.Application.Garage.UpdateVehicle;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;
using Shared.Api.Tenancy;

namespace Customers.Api.Controllers;

/// <summary>
/// The signed-in customer's garage. Only their own cars are visible; anyone else's ID is not found.
/// </summary>
[ApiController]
[Route("api/me/vehicles")]
[Authorize]
[AllowWithoutStudio]
public class MyVehiclesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GarageVehicleDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return await sender.Send(new GetMyVehiclesQuery(), cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<GarageVehicleDto>> Add(
        GarageVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddVehicleCommand(request), cancellationToken);
        if (!result.Success) return this.ToProblem(result.Error!);

        return Created($"/api/me/vehicles/{result.Value!.Id}", result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GarageVehicleDto>> Update(
        Guid id, GarageVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateVehicleCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteVehicleCommand(id), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
