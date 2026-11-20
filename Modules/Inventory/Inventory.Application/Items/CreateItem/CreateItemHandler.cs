using Inventory.Application.Abstractions;
using Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Items.CreateItem;

public sealed record CreateItemCommand(InventoryCreateDto Request) : ICommand<InventoryResponseDto>;

public sealed class CreateItemHandler(IInventoryDbContext db) : ICommandHandler<CreateItemCommand, InventoryResponseDto>
{
    public async ValueTask<InventoryResponseDto> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var item = InventoryItem.Create(
            request.Name, request.Unit, request.Stock, request.MinStock, request.Description, request.CategoryId);

        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        // Load the category so the response carries its name
        await db.Items.Include(i => i.Category).Where(i => i.Id == item.Id).LoadAsync(cancellationToken);

        return InventoryResponseDto.From(item);
    }
}
