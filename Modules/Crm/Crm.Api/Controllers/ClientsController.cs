using Crm.Application.Clients;
using Crm.Application.Clients.CreateClient;
using Crm.Application.Clients.DeleteClient;
using Crm.Application.Clients.GetClient;
using Crm.Application.Clients.GetClients;
using Crm.Application.Clients.UpdateClient;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;
using Shared.Kernel.Pagination;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ClientResponseDto>>> GetAll(
        [FromQuery] PageRequest paging, [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return await sender.Send(new GetClientsQuery(paging, search), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientResponseDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClientQuery(id), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<ClientResponseDto>> Create(
        ClientRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateClientCommand(request), cancellationToken);

        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToProblem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientResponseDto>> Update(
        Guid id, ClientRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateClientCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteClientCommand(id), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
