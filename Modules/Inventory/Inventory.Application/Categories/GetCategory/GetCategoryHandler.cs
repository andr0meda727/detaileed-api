using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Inventory.Application.Categories.GetCategory;

public sealed record GetCategoryQuery(Guid Id) : IQuery<Result<CategoryResponseDto>>;

public sealed class GetCategoryHandler(IInventoryDbContext db) : IQueryHandler<GetCategoryQuery, Result<CategoryResponseDto>>
{
    public async ValueTask<Result<CategoryResponseDto>> Handle(
        GetCategoryQuery query, CancellationToken cancellationToken)
    {
        var id = query.Id;
        var category = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryResponseDto(c.Id, c.Name))
            .FirstOrDefaultAsync(cancellationToken);

        return category == null ? InventoryErrors.CategoryNotFound(id) : category;
    }
}
