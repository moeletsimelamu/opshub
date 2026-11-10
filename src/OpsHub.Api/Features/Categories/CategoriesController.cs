using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;

namespace OpsHub.Api.Features.Categories;

/// <summary>
/// Category management. Separate from GET /api/categories, which every signed-in user calls
/// for the ticket forms and which lists active categories only.
/// </summary>
[Route("api/admin/categories")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CategoriesController(CategoryAdminService categories) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryAdminResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryAdminResponse>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categories.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/admin/categories/{result.Value.Id}", result.Value)
            : Problem(result.Error);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryAdminResponse>> Update(int id, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await categories.UpdateAsync(id, request, cancellationToken));
}
