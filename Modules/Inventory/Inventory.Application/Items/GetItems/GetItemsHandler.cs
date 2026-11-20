using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Pagination;
using Shared.Kernel.Pagination;

namespace Inventory.Application.Items.GetItems;

public sealed record GetItemsQuery(bool LowStockOnly, PageRequest Paging) : IQuery<PagedResponse<InventoryResponseDto>>;

public sealed class GetItemsHandler(IInventoryDbContext db) : IQueryHandler<GetItemsQuery, PagedResponse<InventoryResponseDto>>
{
    public async ValueTask<PagedResponse<InventoryResponseDto>> Handle(
        GetItemsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Items.Include(i => i.Category).AsNoTracking();

        if (request.LowStockOnly)
        {
            // Same rule as InventoryItem.IsLowStock, written so it translates to SQL
            query = query.Where(i => i.MinStock != null && i.Stock < i.MinStock);
        }

        var items = await query
            .OrderBy(i => i.Name).ThenBy(i => i.Id)
            .ToPagedResponseAsync(request.Paging, cancellationToken);

        return items.Map(InventoryResponseDto.From);
    }
}
