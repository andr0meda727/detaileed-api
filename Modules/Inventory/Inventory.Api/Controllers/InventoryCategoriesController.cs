using Inventory.Application.Categories;
using Inventory.Application.Categories.CreateCategory;
using Inventory.Application.Categories.DeleteCategory;
using Inventory.Application.Categories.GetCategories;
using Inventory.Application.Categories.GetCategory;
using Inventory.Application.Categories.UpdateCategory;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/inventory-categories")]
[Authorize]
public class InventoryCategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetCategoriesQuery(), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCategoryQuery(id), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponseDto>> Create(
        [FromBody] CategoryCreateDto request, CancellationToken cancellationToken)
    {
        var category = await sender.Send(new CreateCategoryCommand(request), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponseDto>> Update(
        Guid id, CategoryCreateDto request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateCategoryCommand(id, request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCategoryCommand(id), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
