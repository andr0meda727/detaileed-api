using Inventory.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Categories.GetCategories;

public sealed record GetCategoriesQuery : IQuery<List<CategoryResponseDto>>;

public sealed class GetCategoriesHandler(IInventoryDbContext db) : IQueryHandler<GetCategoriesQuery, List<CategoryResponseDto>>
{
    public async ValueTask<List<CategoryResponseDto>> Handle(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponseDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }
}
