using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Interfaces;

public interface IQuickResponseRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuickResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QuickResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(QuickResponse response, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BatchOperationResult> SetEnabledAsync(IReadOnlyCollection<Guid> ids, bool enabled, CancellationToken cancellationToken = default);
    Task<BatchOperationResult> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<BatchOperationResult> MoveToCategoryAsync(IReadOnlyCollection<Guid> ids, string category, CancellationToken cancellationToken = default);
}
