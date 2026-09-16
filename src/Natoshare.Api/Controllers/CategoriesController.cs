using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers;

// A user's categories: Rent, Feeding, and whatever else they add on top of the
// defaults they got at signup.
[Route("api/v1/categories")]
[Authorize(Policy = "RequireUser")]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateCategoryRequest> _createValidator;
    private readonly IValidator<UpdateCategoryRequest> _updateValidator;
    private readonly IValidator<AddSubCategoryRequest> _addSubCategoryValidator;
    private readonly IValidator<ArchiveCategoryRequest> _archiveValidator;

    public CategoriesController(
        ICategoryService categoryService,
        ICurrentUser currentUser,
        IValidator<CreateCategoryRequest> createValidator,
        IValidator<UpdateCategoryRequest> updateValidator,
        IValidator<AddSubCategoryRequest> addSubCategoryValidator,
        IValidator<ArchiveCategoryRequest> archiveValidator)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addSubCategoryValidator = addSubCategoryValidator;
        _archiveValidator = archiveValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(bool includeArchived, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetCategoriesAsync(_currentUser.UserId, includeArchived, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _categoryService.CreateCategoryAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{categoryId:guid}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _categoryService.UpdateCategoryAsync(_currentUser.UserId, categoryId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{categoryId:guid}/subcategories")]
    public async Task<ActionResult<CategoryDto>> AddSubCategory(Guid categoryId, AddSubCategoryRequest request, CancellationToken cancellationToken)
    {
        await _addSubCategoryValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _categoryService.AddSubCategoryAsync(_currentUser.UserId, categoryId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{categoryId:guid}/subcategories/{name}")]
    public async Task<ActionResult<CategoryDto>> RemoveSubCategory(Guid categoryId, string name, CancellationToken cancellationToken)
    {
        var result = await _categoryService.RemoveSubCategoryAsync(_currentUser.UserId, categoryId, name, cancellationToken);
        return Ok(result);
    }

    // Archiving a category also needs a new split for the money it used to get, so
    // this hands back the new allocation version, not just the category.
    [HttpPost("{categoryId:guid}/archive")]
    public async Task<ActionResult<AllocationVersionResult>> ArchiveCategory(Guid categoryId, ArchiveCategoryRequest request, CancellationToken cancellationToken)
    {
        await _archiveValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _categoryService.ArchiveCategoryAsync(_currentUser.UserId, categoryId, request, cancellationToken);
        return Ok(result);
    }
}
