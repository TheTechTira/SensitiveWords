using Microsoft.Extensions.Options;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Common.Enums;
using SensitiveWords.Application.Common.Options;
using SensitiveWords.Application.Common.Results;
using SensitiveWords.Application.Extensions;
using SensitiveWords.Application.Mappers;
using SensitiveWords.Domain.Dtos;

namespace SensitiveWords.Application.Services
{
    /// <summary>
    /// Application-layer service for querying and managing sensitive words.
    ///
    /// For the next dev:
    /// - This layer owns *business* concerns (policy checks, light normalization),
    ///   not persistence; persistence lives behind ISensitiveWordRepository.
    /// - We return ServiceResult<T> to standardize happy/edge/error flows across the app.
    /// - DTOs at the service boundary keep controllers thin and prevent entity leakage.
    ///
    /// Interview notes (why I chose this design):
    /// - Separation of concerns: repository exposes facts; service enforces rules.
    /// - Explicit status mapping (Repo -> Service) makes error semantics discoverable
    ///   and keeps API/controller code boring.
    /// - Policy is injected (IOptions) so we can swap to IOptionsMonitor for hot-reload
    ///   without touching call sites.
    /// - Idempotent create via CreateOrReviveAsync reduces client complexity
    ///   and protects against retries.
    /// </summary>
    public class SensitiveWordService : ISensitiveWordService
    {
        private readonly ISensitiveWordRepository _repo;

        // Rationale: HashSet + OrdinalIgnoreCase for O(1) lookups and predictable casing semantics.
        // This is read-only after construction, so it's thread-safe without additional locking.
        private readonly HashSet<string> _blockedWords;

        /// <summary>
        /// Ctor wires repo and policy. If we need dynamic policy reloads later,
        /// swap IOptions for IOptionsMonitor and rebuild the set on change.
        /// </summary>
        public SensitiveWordService(
            ISensitiveWordRepository repo,
            IOptions<SensitiveWordPolicyOptions> sensitiveWordPolicyOptions)
        {
            _repo = repo;
            _blockedWords = new(
                sensitiveWordPolicyOptions.Value.BlockedWords ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// List words with optional search.
        /// - Paging/clamping is enforced in the repo (single source of truth).
        /// - We map domain -> DTO here and override the success message for consistency.
        /// </summary>
        public async Task<ServiceResult<PagedResult<SensitiveWordDto>>> ListAsync(
            int page, int pageSize, string? search, CancellationToken ct)
        {
            var repoRes = await _repo.ListAsync(page, pageSize, search, ct);

            // Why: let repo handle NotFound/Error/Invalid; we just set a consistent success message.
            return repoRes
                .ToServiceResultPaged(DomainToDtoMapper.MapSensitiveWordDto)
                .WithMessage("Words listed.");
        }

        /// <summary>
        /// Get a single word by Id.
        /// - Status (NotFound/Error/etc.) bubbles from repo; we only brand the success message.
        /// </summary>
        public async Task<ServiceResult<SensitiveWordDto>> GetAsync(int id, CancellationToken ct)
        {
            var repoRes = await _repo.GetAsync(id, ct);

            return repoRes
                .ToServiceResult(DomainToDtoMapper.MapSensitiveWordDto)
                .WithMessage("Word fetched successfully.");
        }

        /// <summary>
        /// Create or revive a word (idempotent upsert intent).
        /// Steps:
        /// 1) Transport validation lives at the edge (e.g., controllers via DataAnnotations).
        /// 2) Light business normalization (Trim).
        /// 3) Enforce policy (blocked words).
        /// 4) Persist via CreateOrRevive to keep clients simple and handle retries safely.
        /// </summary>
        public async Task<ServiceResult<int>> CreateAsync(string word, bool isActive, CancellationToken ct)
        {
            // (1) Transport checks are assumed handled by the API layer.

            // (2) Light normalization; deeper Unicode normalization can be added later if needed.
            var normalized = word.Trim();

            // (3) Policy enforcement at the service boundary keeps persistence generic.
            if (_blockedWords.Contains(normalized))
                return ServiceResult<int>.Invalid("Word is not allowed by policy.", "word_blocked");

            // (4) Idempotent create: revives soft-deleted entries and avoids duplicate errors on retries.
            var repoRes = await _repo.CreateOrReviveAsync(normalized, isActive, ct);

            // Rationale: explicit mapping gives us copy you can localize/change without affecting repository.
            return repoRes.Status switch
            {
                EnumRepositoryResultStatus.Ok => ServiceResult<int>.Ok(repoRes.Data, "Word created or revived."),
                EnumRepositoryResultStatus.Conflict => ServiceResult<int>.Conflict(repoRes.Message ?? "Word already exists.", repoRes.ErrorCode ?? "word_conflict"),
                EnumRepositoryResultStatus.Invalid => ServiceResult<int>.Invalid(repoRes.Message ?? "Invalid word.", repoRes.ErrorCode),
                _ => ServiceResult<int>.Error(repoRes.Message ?? "Failed to create word.", repoRes.ErrorCode)
            };

            // Alternative (more DRY, less explicit):
            // return repoRes.ToServiceResult().WithMessage("Word created or revived.");
        }

        /// <summary>
        /// Update value/active flag.
        /// - Performs minimal validation/normalization here (business-facing).
        /// - Maps repository outcomes into service outcomes/messages for the API.
        /// </summary>
        public async Task<ServiceResult<bool>> UpdateAsync(int id, string word, bool isActive, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(word))
                return ServiceResult<bool>.Invalid("Word is required.", "word_required");

            var normalized = word.Trim();
            var res = await _repo.UpdateAsync(id, normalized, isActive, ct);

            return res.Status switch
            {
                EnumRepositoryResultStatus.Ok => ServiceResult<bool>.Ok(true, "Word updated."),
                EnumRepositoryResultStatus.NotFound => ServiceResult<bool>.NotFound("Word not found."),
                EnumRepositoryResultStatus.Conflict => ServiceResult<bool>.Conflict(res.Message ?? "Another word with the same value exists.", "word_conflict"),
                EnumRepositoryResultStatus.Invalid => ServiceResult<bool>.Invalid(res.Message ?? "Invalid update.", res.ErrorCode),
                _ => ServiceResult<bool>.Error(res.Message ?? "Failed to update word.", res.ErrorCode)
            };
        }

        /// <summary>
        /// Delete word (hard or soft).
        /// - We invert the arg: repo expects softDelete; API exposes hardDelete for clarity.
        /// - Prefers soft delete by default so we can revive later and preserve audit history.
        /// </summary>
        public async Task<ServiceResult<bool>> DeleteAsync(int id, bool hardDelete, CancellationToken ct)
        {
            var res = await _repo.DeleteAsync(id, softDelete: !hardDelete, ct);

            return res.Status switch
            {
                EnumRepositoryResultStatus.Ok => ServiceResult<bool>.Ok(true, hardDelete ? "Word deleted." : "Word soft-deleted."),
                EnumRepositoryResultStatus.NotFound => ServiceResult<bool>.NotFound("Word not found."),
                _ => ServiceResult<bool>.Error(res.Message ?? "Failed to delete word.", res.ErrorCode)
            };
        }
    }
}
