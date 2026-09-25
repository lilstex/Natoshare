namespace Natoshare.Application.Budgeting;

// Managing a user's categories: Rent, Feeding, and whatever else they set up.
public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateCategoryAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> AddSubCategoryAsync(Guid userId, Guid categoryId, AddSubCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> RemoveSubCategoryAsync(Guid userId, Guid categoryId, string name, CancellationToken cancellationToken = default);

    // Archiving a category needs a new split for whatever is left active, so the
    // remaining percentages still add up to 100.
    Task<AllocationVersionResult> ArchiveCategoryAsync(Guid userId, Guid categoryId, ArchiveCategoryRequest request, CancellationToken cancellationToken = default);
}
