using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Inventory.Application.Items.GetItem;

public sealed record GetItemQuery(Guid Id) : IQuery<Result<InventoryResponseDto>>;

public sealed class GetItemHandler(IInventoryDbContext db) : IQueryHandler<GetItemQuery, Result<InventoryResponseDto>>
{
    public async ValueTask<Result<InventoryResponseDto>> Handle(GetItemQuery query, CancellationToken cancellationToken)
    {
        var id = query.Id;
        var item = await db.Items
            .Include(i => i.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return item == null ? InventoryErrors.ItemNotFound(id) : InventoryResponseDto.From(item);
    }
}
