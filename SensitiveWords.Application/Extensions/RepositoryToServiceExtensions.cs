using SensitiveWords.Application.Common.Enums;
using SensitiveWords.Application.Common.Results;

namespace SensitiveWords.Application.Extensions
{
    public static class RepositoryToServiceExtensions
    {
        /// <summary>
        /// Bridge a RepositoryResult<T> to a ServiceResult<T> with the same payload.
        /// </summary>
        public static ServiceResult<T> ToServiceResult<T>(this RepositoryResult<T> repoResult)
        {
            return repoResult.Status switch
            {
                EnumRepositoryResultStatus.Ok =>
                    ServiceResult<T>.Ok(repoResult.Data, repoResult.Message),

                EnumRepositoryResultStatus.NotFound =>
                    ServiceResult<T>.NotFound(repoResult.Message ?? "Resource not found.", repoResult.ErrorCode),

                EnumRepositoryResultStatus.Invalid =>
                    ServiceResult<T>.Invalid(repoResult.Message ?? "Invalid request.", repoResult.ErrorCode),

                EnumRepositoryResultStatus.Conflict =>
                    ServiceResult<T>.Conflict(repoResult.Message ?? "Conflict.", repoResult.ErrorCode),

                _ =>
                    ServiceResult<T>.Error(repoResult.Message ?? "Internal error.", repoResult.ErrorCode)
            };
        }

        /// <summary>
        /// Bridge + map: RepositoryResult<TIn> → ServiceResult<TOut>.
        /// If the repo result is not OK (or has null Data), the error status/message/code are "bubbled" through.
        /// </summary>
        public static ServiceResult<TOut> ToServiceResult<TIn, TOut>(
            this RepositoryResult<TIn> repoResult,
            Func<TIn, TOut> mapper)
        {
            if (repoResult.Status != EnumRepositoryResultStatus.Ok || repoResult.Data == null)
            {
                return repoResult.BubbleError<TIn, TOut>();
            }

            return ServiceResult<TOut>.Ok(mapper(repoResult.Data), repoResult.Message);
        }

        /// <summary>
        /// Bridge + map for paged payloads: RepositoryResult<PagedResult<TIn>> → ServiceResult<PagedResult<TOut>>.
        /// </summary>
        public static ServiceResult<PagedResult<TOut>> ToServiceResultPaged<TIn, TOut>(
            this RepositoryResult<PagedResult<TIn>> repoResult,
            Func<TIn, TOut> mapper)
        {
            if (repoResult.Status != EnumRepositoryResultStatus.Ok || repoResult.Data == null)
            {
                return repoResult.BubbleError<PagedResult<TIn>, PagedResult<TOut>>();
            }

            var dtoPage = repoResult.Data.Map(mapper);
            return ServiceResult<PagedResult<TOut>>.Ok(dtoPage, repoResult.Message);
        }

        // ---- private helpers -------------------------------------------------

        /// <summary>
        /// Bubble error/invalid/notfound/conflict from RepositoryResult<TIn> to ServiceResult<TOut> (when types differ).
        /// </summary>
        private static ServiceResult<TOut> BubbleError<TIn, TOut>(this RepositoryResult<TIn> repoResult)
        {
            return repoResult.Status switch
            {
                EnumRepositoryResultStatus.NotFound =>
                    ServiceResult<TOut>.NotFound(repoResult.Message ?? "Resource not found.", repoResult.ErrorCode),

                EnumRepositoryResultStatus.Invalid =>
                    ServiceResult<TOut>.Invalid(repoResult.Message ?? "Invalid request.", repoResult.ErrorCode),

                EnumRepositoryResultStatus.Conflict =>
                    ServiceResult<TOut>.Conflict(repoResult.Message ?? "Conflict.", repoResult.ErrorCode),

                _ =>
                    ServiceResult<TOut>.Error(repoResult.Message ?? "Internal error.", repoResult.ErrorCode)
            };
        }
    }

}
