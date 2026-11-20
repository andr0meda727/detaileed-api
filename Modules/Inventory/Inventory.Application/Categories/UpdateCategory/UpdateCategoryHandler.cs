using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Inventory.Application.Categories.UpdateCategory;

public sealed record UpdateCategoryCommand(Guid Id, CategoryCreateDto Request) : ICommand<Result<CategoryResponseDto>>;

public sealed class UpdateCategoryHandler(IInventoryDbContext db)
    : ICommandHandler<UpdateCategoryCommand, Result<CategoryResponseDto>>
{
    public async ValueTask<Result<CategoryResponseDto>> Handle(
        UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null) return InventoryErrors.CategoryNotFound(id);

        category.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);

        return CategoryResponseDto.From(category);
    }
}
