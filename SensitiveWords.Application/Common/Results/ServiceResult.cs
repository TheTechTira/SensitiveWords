using SensitiveWords.Application.Common.Enums;

namespace SensitiveWords.Application.Common.Results
{
    /// <summary>
    /// Standardized application-layer result wrapper.
    /// Encodes outcome (<see cref="EnumServiceResultStatus"/>), optional human message,
    /// optional machine-readable error code, and an optional payload.
    ///
    /// Why this exists:
    /// - Keeps controllers/API endpoints boring and consistent (switch on Status).
    /// - Separates human-facing <see cref="Message"/> from machine-facing <see cref="ErrorCode"/>.
    /// - Immutable “factory” creation clarifies intent and avoids partially-filled objects.
    ///
    /// Guidance for next dev:
    /// - Prefer these factories (<see cref="Ok(T?, string?)"/>, <see cref="NotFound(string?, string?)"/>, etc.)
    ///   over `new ServiceResult<T> { ... }` for consistency.
    /// - Keep <see cref="Message"/> short and user-friendly; log details elsewhere.
    /// - Use <see cref="ErrorCode"/> for programmatic branching (e.g., "word_conflict").
    /// </summary>
    public class ServiceResult<T>
    {
        /// <summary>
        /// Outcome of the operation (Ok, NotFound, Conflict, Invalid, Error).
        /// </summary>
        public EnumServiceResultStatus Status { get; init; }

        /// <summary>
        /// Human-readable message suitable for clients/logs. Optional.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Machine-readable error code (e.g., "word_conflict", "db_error"). Optional.
        /// Consumers should branch on this rather than parsing <see cref="Message"/>.
        /// </summary>
        public string? ErrorCode { get; init; }

        /// <summary>
        /// Optional payload (entity/DTO/primitive). Null for flows that don't return data.
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// Success result, with optional payload and message.
        /// </summary>
        public static ServiceResult<T> Ok(T? data = default, string? msg = null)
            => new() { Status = EnumServiceResultStatus.Ok, Data = data, Message = msg };

        /// <summary>
        /// Not-found result (e.g., missing resource).
        /// </summary>
        public static ServiceResult<T> NotFound(string? msg = null, string? code = null)
            => new() { Status = EnumServiceResultStatus.NotFound, Message = msg, ErrorCode = code };

        /// <summary>
        /// Conflict result (e.g., uniqueness collision).
        /// </summary>
        public static ServiceResult<T> Conflict(string msg, string? code = null)
            => new() { Status = EnumServiceResultStatus.Conflict, Message = msg, ErrorCode = code };

        /// <summary>
        /// Invalid result (validation/business rule failure).
        /// </summary>
        public static ServiceResult<T> Invalid(string msg, string? code = null)
            => new() { Status = EnumServiceResultStatus.Invalid, Message = msg, ErrorCode = code };

        /// <summary>
        /// Unexpected failure (I/O/infra/unknown).
        /// </summary>
        public static ServiceResult<T> Error(string msg, string? code = null)
            => new() { Status = EnumServiceResultStatus.Error, Message = msg, ErrorCode = code };
    }
}
