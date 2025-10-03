using SensitiveWords.Application.Common.Results;
using SensitiveWords.Domain.Entities;

namespace SensitiveWords.Application.Abstractions.Repositories
{
    public interface ISensitiveWordRepository
    {
        Task<RepositoryResult<int>> CreateAsync(string word, bool isActive, CancellationToken ct);
        Task<RepositoryResult<int>> CreateOrReviveAsync(string word, bool isActive, CancellationToken ct);
        Task<RepositoryResult<bool>> UpdateAsync(int id, string word, bool isActive, CancellationToken ct);
        Task<RepositoryResult<bool>> DeleteAsync(int id, bool softDelete, CancellationToken ct);
        Task<RepositoryResult<SensitiveWord>> GetAsync(int id, CancellationToken ct);
        Task<RepositoryResult<PagedResult<SensitiveWord>>> ListAsync(int page, int pageSize, string? search, CancellationToken ct); // IsDeleted=0 AND IsActive=1
        Task<RepositoryResult<IReadOnlyList<SensitiveWord>>> ListActiveAsync(CancellationToken ct);

        Task<int> GetWordsVersionAsync(CancellationToken ct);
        Task IncrementWordsVersionAsync(CancellationToken ct);
        Task BulkUpsertAsync(IEnumerable<string> words, CancellationToken ct);
    }
}