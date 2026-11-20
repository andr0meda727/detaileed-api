using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Inventory.Application.Items.UpdateItem;

public sealed record UpdateItemCommand(Guid Id, InventoryCreateDto Request) : ICommand<Result<InventoryResponseDto>>;

public sealed class UpdateItemHandler(IInventoryDbContext db) : ICommandHandler<UpdateItemCommand, Result<InventoryResponseDto>>
{
    public async ValueTask<Result<InventoryResponseDto>> Handle(
        UpdateItemCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        // Tenant-filtered, so an ID from another studio is treated as not found
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null) return InventoryErrors.ItemNotFound(id);

        item.Update(request.Name, request.Unit, request.Stock, request.MinStock, request.Description, request.CategoryId);
        await db.SaveChangesAsync(cancellationToken);

        await db.Items.Include(i => i.Category).Where(i => i.Id == id).LoadAsync(cancellationToken);

        return InventoryResponseDto.From(item);
    }
}
