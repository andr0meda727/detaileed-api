using Inventory.Application.Abstractions;
using Inventory.Domain;
using Mediator;

namespace Inventory.Application.Categories.CreateCategory;

public sealed record CreateCategoryCommand(CategoryCreateDto Request) : ICommand<CategoryResponseDto>;

public sealed class CreateCategoryHandler(IInventoryDbContext db) : ICommandHandler<CreateCategoryCommand, CategoryResponseDto>
{
    public async ValueTask<CategoryResponseDto> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var category = InventoryCategory.Create(request.Name);

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return CategoryResponseDto.From(category);
    }
}
