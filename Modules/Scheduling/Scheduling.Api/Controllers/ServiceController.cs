using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Application.Catalog;
using Scheduling.Application.Catalog.CreateService;
using Scheduling.Application.Catalog.DeleteService;
using Scheduling.Application.Catalog.GetServices;
using Scheduling.Application.Catalog.UpdateService;
using Shared.Api.Errors;

namespace Scheduling.Api.Controllers;

// Singular route kept for existing clients of the API
[ApiController]
[Route("api/service")]
[Authorize]
public class ServiceController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ServiceResponseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return await sender.Send(new GetServicesQuery(), cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceResponseDto>> Create(
        ServiceRequest request, CancellationToken cancellationToken)
    {
        return await sender.Send(new CreateServiceCommand(request), cancellationToken);
    }

    /// <summary>
    /// Changes the name, category and prices. Left out, the category and prices stay as they are.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ServiceResponseDto>> Update(
        Guid id, ServiceRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateServiceCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteServiceCommand(id), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
