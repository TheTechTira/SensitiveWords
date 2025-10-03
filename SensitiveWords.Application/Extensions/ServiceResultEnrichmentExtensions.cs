using SensitiveWords.Application.Common.Enums;
using SensitiveWords.Application.Common.Results;

namespace SensitiveWords.Application.Extensions
{
    /// <summary>
    /// Convenience helpers for enriching <see cref="ServiceResult{T}"/> instances.
    /// </summary>
    public static class ServiceResultEnrichmentExtensions
    {
        /// <summary>
        /// If the result is <see cref="EnumServiceResultStatus.Ok"/>, returns a new <c>Ok</c> result
        /// with the same <c>Data</c> and a custom success message. For any non-OK status, returns
        /// the original result untouched.
        /// 
        /// Why:
        /// - Keeps call sites clean: <c>repoRes.ToServiceResult(...).WithMessage("Created!")</c>.
        /// - Avoids accidentally changing error messages or statuses coming from lower layers.
        /// 
        /// Caveat:
        /// - This recreates the <c>Ok</c> result via <c>ServiceResult&lt;T&gt;.Ok(data, message)</c>.
        ///   If your <c>ServiceResult&lt;T&gt;</c> carries extra metadata on success (e.g., <c>Affected</c>,
        ///   correlation IDs, etc.) and your <c>Ok(...)</c> factory doesn’t accept them, they won’t be
        ///   preserved by this helper. If you need to keep that metadata, consider adding an overload
        ///   to <c>Ok(...)</c> that accepts it, or provide a dedicated <c>WithOkMessagePreserve(...)</c>.
        /// </summary>
        public static ServiceResult<T> WithMessage<T>(this ServiceResult<T> result, string messageWhenOk)
            => result.Status == EnumServiceResultStatus.Ok
               ? ServiceResult<T>.Ok(result.Data, messageWhenOk)
               : result; // leave non-OK untouched
    }
}
