using SensitiveWords.Application.Common.Results;
using SensitiveWords.Domain.Entities;

namespace SensitiveWords.Application.Abstractions.Repositories
{
    /// <summary>
    /// Persistence boundary for <see cref="SensitiveWord"/> aggregate.
    ///
    /// Notes for the next dev:
    /// - All methods return <see cref="RepositoryResult{T}"/> so callers can branch on
    ///   <c>Status</c> (Ok/NotFound/Conflict/Invalid/Error) without throwing for expected outcomes.
    /// - Mutations (create/update/delete/bulk) are expected to bump a monotonic
    ///   "WordsVersion" value used by readers to invalidate caches.
    /// - Methods dealing with lists never return null collections; empty lists are fine.
    /// - Case-insensitive comparisons are used where noted (e.g., create-or-revive).
    /// </summary>
    public interface ISensitiveWordRepository
    {
        /// <summary>
        /// Inserts a new word.
        /// Use <see cref="CreateOrReviveAsync"/> if you want to revive an existing soft-deleted word.
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(id) on success; <c>Invalid</c>/<c>Error</c> otherwise.
        /// Implementations should bump WordsVersion on success.
        /// </returns>
        Task<RepositoryResult<int>> CreateAsync(string word, bool isActive, CancellationToken ct);

        /// <summary>
        /// Creates a word if it doesn't exist, or revives a matching (case-insensitive) existing/soft-deleted row.
        /// Also updates <c>IsActive</c> when reviving.
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(id) on success; <c>Invalid</c>/<c>Error</c> on failure.
        /// Implementations should bump WordsVersion on success.
        /// </returns>
        Task<RepositoryResult<int>> CreateOrReviveAsync(string word, bool isActive, CancellationToken ct);

        /// <summary>
        /// Updates a word's value and active flag.
        /// Should enforce uniqueness and return <c>Conflict</c> when renaming to an existing non-deleted value.
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(true) on success, <c>NotFound</c> if missing, <c>Conflict</c> on duplicate,
        /// <c>Invalid</c>/<c>Error</c> on other failures. Should bump WordsVersion on success.
        /// </returns>
        Task<RepositoryResult<bool>> UpdateAsync(int id, string word, bool isActive, CancellationToken ct);

        /// <summary>
        /// Deletes a word.
        /// If <paramref name="softDelete"/> is true, marks the row deleted (preferred); otherwise hard-deletes.
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(true) on success, <c>NotFound</c> if missing, or <c>Error</c> on failures.
        /// Should bump WordsVersion on success.
        /// </returns>
        Task<RepositoryResult<bool>> DeleteAsync(int id, bool softDelete, CancellationToken ct);

        /// <summary>
        /// Gets a single non-deleted word by Id.
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(entity) when found; <c>NotFound</c> if missing; <c>Error</c> on failures.
        /// </returns>
        Task<RepositoryResult<SensitiveWord>> GetAsync(int id, CancellationToken ct);

        /// <summary>
        /// Lists non-deleted words with optional case-insensitive LIKE search on <c>Word</c>.
        /// Paging is 1-based and clamped by the implementation (e.g., max pageSize).
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(paged payload) on success; <c>Error</c> on failures.
        /// </returns>
        Task<RepositoryResult<PagedResult<SensitiveWord>>> ListAsync(int page, int pageSize, string? search, CancellationToken ct);

        /// <summary>
        /// Lists all <c>IsActive = 1</c> and non-deleted words.
        /// </summary>
        /// <returns>
        /// <c>Ok</c>(list) on success (possibly empty); <c>Error</c> on failures.
        /// </returns>
        Task<RepositoryResult<IReadOnlyList<SensitiveWord>>> ListActiveAsync(CancellationToken ct);

        /// <summary>
        /// Returns the current monotonic "WordsVersion" (0 if not set).
        /// Readers can cache word lists keyed by this value and refresh when it changes.
        /// </summary>
        Task<int> GetWordsVersionAsync(CancellationToken ct);

        /// <summary>
        /// Increments the "WordsVersion" counter (upserts if missing).
        /// Implementations typically call this internally after successful mutations.
        /// </summary>
        Task IncrementWordsVersionAsync(CancellationToken ct);

        /// <summary>
        /// Efficiently upserts a batch of words.
        /// Typical behavior:
        /// - Insert new words as active.
        /// - Reactivate existing inactive words.
        /// - Ignore empty/duplicate inputs (normalization left to implementation).
        /// Should bump WordsVersion on success.
        /// </summary>
        Task BulkUpsertAsync(IEnumerable<string> words, CancellationToken ct);
    }
}