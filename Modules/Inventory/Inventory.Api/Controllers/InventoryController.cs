using Inventory.Application.Items;
using Inventory.Application.Items.CreateItem;
using Inventory.Application.Items.GetItem;
using Inventory.Application.Items.GetItems;
using Inventory.Application.Items.UpdateItem;
using Inventory.Application.Stock;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;
using Shared.Kernel.Pagination;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<InventoryResponseDto>>> GetAll(
        [FromQuery] bool? isLowStock, [FromQuery] PageRequest paging, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetItemsQuery(isLowStock == true, paging), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetItemQuery(id), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<InventoryResponseDto>> Create(
        InventoryCreateDto request, CancellationToken cancellationToken)
    {
        return await sender.Send(new CreateItemCommand(request), cancellationToken);
    }

    /// <summary>
    /// A delivery (<c>Purchase</c>, Quantity added) or a stocktake (<c>Stocktake</c>, Quantity counted).
    /// </summary>
    [HttpPost("{id:guid}/movements")]
    public async Task<ActionResult<InventoryResponseDto>> RecordStockChange(
        Guid id, StockChangeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RecordStockChangeCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// The item's stock history, newest first: deliveries, stocktakes and what visits used.
    /// </summary>
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<PagedResponse<StockMovementDto>>> GetMovements(
        Guid id, [FromQuery] PageRequest paging, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStockMovementsQuery(id, paging), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// Edits the item. A different stock than now is recorded as a stocktake.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InventoryResponseDto>> Update(
        Guid id, InventoryCreateDto request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateItemCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }
}
