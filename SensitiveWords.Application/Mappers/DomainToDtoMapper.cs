using SensitiveWords.Domain.Dtos;
using SensitiveWords.Domain.Entities;

namespace SensitiveWords.Application.Mappers
{
    /// <summary>
    /// Centralized domain → DTO mapping helpers.
    ///
    /// Why manual mapping (vs. AutoMapper/etc.):
    /// - This DTO is tiny and hot-path; explicit mapping is faster, clearer, and easy to grep.
    /// - Keeps compile-time safety when fields are added/renamed (you’ll get build errors).
    /// - Avoids a runtime profile/config layer for something simple.
    ///
    /// Design notes for the next dev:
    /// - Keep these methods *pure* (no I/O, no time lookups, no side effects).
    /// - Keep *business decisions* out of mapping (no trimming/normalizing/formatting here).
    ///   Do that in the service/validation layers. Mapping should be shape-only.
    /// - If this grows, prefer small, explicit mappers per aggregate/DTO pair over a mega “Mapper”.
    /// </summary>
    public static class DomainToDtoMapper
    {
        /// <summary>
        /// Projects a <see cref="SensitiveWord"/> entity to a <see cref="SensitiveWordDto"/>.
        /// </summary>
        /// <param name="domainEntity">The source domain entity (non-null).</param>
        /// <returns>A DTO carrying the fields relevant to API/UI.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="domainEntity"/> is null.</exception>
        public static SensitiveWordDto MapSensitiveWordDto(SensitiveWord domainEntity)
        {
            if (domainEntity is null) throw new ArgumentNullException(nameof(domainEntity));

            // Shape-only mapping: business normalization/formatting is not done here.
            return new SensitiveWordDto(
                id: domainEntity.Id,
                word: domainEntity.Word,
                isActive: domainEntity.IsActive
            );
        }

        // Example: if you later add timestamps to the DTO, extend explicitly here.
        // public static SensitiveWordDto MapSensitiveWordDto(SensitiveWord e) => new(
        //     id: e.Id,
        //     word: e.Word,
        //     isActive: e.IsActive,
        //     createdAtUtc: e.CreatedAtUtc,
        //     updatedAtUtc: e.UpdatedAtUtc
        // );
    }
}