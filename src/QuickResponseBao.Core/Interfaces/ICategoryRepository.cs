using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<CategoryInfo> AddCategoryAsync(string name, CancellationToken cancellationToken = default);
    Task RenameCategoryAsync(Guid id, string newName, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(Guid id, bool moveResponsesToUncategorized, CancellationToken cancellationToken = default);
    Task ReorderCategoriesAsync(IReadOnlyList<Guid> categoryIds, CancellationToken cancellationToken = default);
    Task<int> CountResponsesAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
