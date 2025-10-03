using SensitiveWords.Application.Common.Results;
using SensitiveWords.Domain.Dtos;

namespace SensitiveWords.Application.Abstractions.Services
{
    /// <summary>
    /// Application-level service for querying and managing sensitive words.
    /// 
    /// Notes:
    /// - Returns <see cref="ServiceResult{T}"/> to standardize success/error outcomes for callers (e.g., API).
    /// - Methods operate on DTOs (<see cref="SensitiveWordDto"/>) rather than entities.
    /// - Paging is 1-based (page >= 1). Implementations may clamp invalid inputs.
    /// - The <c>search</c> parameter (when supported) typically applies a case-insensitive LIKE on the word value.
    /// </summary>
    public interface ISensitiveWordService
    {
        /// <summary>
        /// Returns a paged list of sensitive words (DTOs), optionally filtered by a search term.
        /// </summary>
        /// <param name="page">1-based page index. Implementations may clamp values &lt; 1 to 1.</param>
        /// <param name="pageSize">Items per page. Implementations may clamp to a sane range (e.g., 1..200).</param>
        /// <param name="search">Optional filter term applied to the word (e.g., SQL LIKE '%term%').</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <see cref="ServiceResult{T}"/> containing a <see cref="PagedResult{T}"/> of <see cref="SensitiveWordDto"/>.
        /// On success: Status=Ok, Data populated; on failure: appropriate status/message/code.
        /// </returns>
        Task<ServiceResult<PagedResult<SensitiveWordDto>>> ListAsync(int page, int pageSize, string? search, CancellationToken ct);

        /// <summary>
        /// Gets a single sensitive word by Id.
        /// </summary>
        /// <param name="id">The word identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// On success: Status=Ok with the DTO; if missing: Status=NotFound; on error: Status=Error with details.
        /// </returns>
        Task<ServiceResult<SensitiveWordDto>> GetAsync(int id, CancellationToken ct);

        /// <summary>
        /// Creates a new sensitive word.
        /// </summary>
        /// <param name="word">The word value to create.</param>
        /// <param name="isActive">Whether the word should be active upon creation.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// On success: Status=Ok with the new Id; on validation/duplication issues: Status=Invalid/Conflict; on error: Status=Error.
        /// </returns>
        Task<ServiceResult<int>> CreateAsync(string word, bool isActive, CancellationToken ct);

        /// <summary>
        /// Updates an existing word's value and active flag.
        /// </summary>
        /// <param name="id">The word identifier.</param>
        /// <param name="word">New word value.</param>
        /// <param name="isActive">New active flag.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// On success: Status=Ok (true); if not found: Status=NotFound; if renaming conflicts: Status=Conflict; on error: Status=Error.
        /// </returns>
        Task<ServiceResult<bool>> UpdateAsync(int id, string word, bool isActive, CancellationToken ct);

        /// <summary>
        /// Deletes a word by Id.
        /// </summary>
        /// <param name="id">The word identifier.</param>
        /// <param name="hardDelete">
        /// If true, permanently removes the row. If false, performs a soft delete (marks as deleted/inactive)
        /// to allow potential revival later.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// On success: Status=Ok (true); if not found: Status=NotFound; on error: Status=Error.
        /// </returns>
        Task<ServiceResult<bool>> DeleteAsync(int id, bool hardDelete, CancellationToken ct);
    }
}
