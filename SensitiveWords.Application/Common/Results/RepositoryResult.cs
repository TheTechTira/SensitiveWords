using SensitiveWords.Application.Common.Enums;

namespace SensitiveWords.Application.Common.Results
{
    /// <summary>
    /// Standard wrapper for repository operations.
    /// Encapsulates the result status, optional data, messages, error codes,
    /// and number of affected rows (when applicable).
    /// 
    /// Usage:
    /// - Use the static helpers (Ok, NotFound, Conflict, Invalid, Error)
    ///   instead of manually constructing.
    /// - Allows services/controllers to handle outcomes consistently.
    /// </summary>
    public sealed class RepositoryResult<T>
    {
        /// <summary>
        /// Overall outcome of the repository call (Ok, NotFound, Conflict, etc).
        /// </summary>
        public EnumRepositoryResultStatus Status { get; init; }

        /// <summary>
        /// Optional human-readable message (success/failure description).
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Optional machine-readable error code (e.g. "word_conflict", "db_error").
        /// Helps distinguish between failure cases programmatically.
        /// </summary>
        public string? ErrorCode { get; init; }

        /// <summary>
        /// Rows affected by the operation (e.g. number of rows updated/deleted).
        /// Defaults to 0 if not relevant.
        /// </summary>
        public int Affected { get; init; }

        /// <summary>
        /// The actual data returned, if any (e.g. entity, list, paged result).
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// Success result, with optional data, affected rows, and message.
        /// </summary>
        public static RepositoryResult<T> Ok(T? data = default, int affected = 0, string? msg = null)
            => new() { Status = EnumRepositoryResultStatus.Ok, Data = data, Affected = affected, Message = msg };

        /// <summary>
        /// Not found result. Use when the target row/entity doesn’t exist.
        /// </summary>
        public static RepositoryResult<T> NotFound(string? msg = null)
            => new() { Status = EnumRepositoryResultStatus.NotFound, Message = msg };

        /// <summary>
        /// Conflict result. Use when a uniqueness constraint is violated or
        /// an entity already exists in a conflicting state.
        /// </summary>
        public static RepositoryResult<T> Conflict(string msg, string? code = null)
            => new() { Status = EnumRepositoryResultStatus.Conflict, Message = msg, ErrorCode = code };

        /// <summary>
        /// Invalid result. Use when the input data is invalid or fails validation.
        /// </summary>
        public static RepositoryResult<T> Invalid(string msg, string? code = null)
            => new() { Status = EnumRepositoryResultStatus.Invalid, Message = msg, ErrorCode = code };

        /// <summary>
        /// Generic error result. Use for unexpected failures (e.g. DB errors).
        /// </summary>
        public static RepositoryResult<T> Error(string msg, string? code = null)
            => new() { Status = EnumRepositoryResultStatus.Error, Message = msg, ErrorCode = code };
    }
}
