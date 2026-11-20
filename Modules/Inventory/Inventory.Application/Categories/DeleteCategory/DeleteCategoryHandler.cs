using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Inventory.Application.Categories.DeleteCategory;

public sealed record DeleteCategoryCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteCategoryHandler(IInventoryDbContext db) : ICommandHandler<DeleteCategoryCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null) return InventoryErrors.CategoryNotFound(id);

        // Items in the category keep existing; the database sets their category_id to NULL
        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
