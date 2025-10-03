namespace SensitiveWords.Application.Common.Responses
{
    /// <summary>
    /// Standard success envelope for API responses.
    /// 
    /// Why:
    /// - Gives clients a predictable shape (data + message + trace id + metadata).
    /// - Keeps error envelopes separate (e.g., ProblemDetails) so success stays clean.
    /// 
    /// Notes for the next dev:
    /// - <typeparamref name="T"/> is the primary payload; use <c>T = object</c> or a DTO type.
    /// - <see cref="TraceId"/> should come from ASP.NET Core (e.g., <c>HttpContext.TraceIdentifier</c>
    ///   or <c>Activity.Current?.Id</c>) to correlate logs.
    /// - <see cref="Meta"/> is for small, JSON-serializable extras like paging, version, links.
    ///   Avoid putting large blobs here; prefer to model them in <typeparamref name="T"/>.
    /// </summary>
    public class SuccessResponse<T>
    {
        /// <summary>
        /// Primary payload. Null when the endpoint has nothing to return but still succeeded
        /// (e.g., create/update operations where only the message is relevant).
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// Optional human-friendly message suitable for UI display or logs
        /// (e.g., "Words listed.", "Word created.").
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Correlation identifier for tracing. Populate from
        /// <c>HttpContext.TraceIdentifier</c> or <c>Activity.Current?.Id</c>.
        /// Defaults to empty string if not set.
        /// </summary>
        public string TraceId { get; init; } = "";

        /// <summary>
        /// Optional small metadata object (e.g., <c>{ page, pageSize, totalCount }</c>,
        /// <c>{ version }</c>, or HATEOAS links). Must be JSON-serializable.
        /// </summary>
        public object? Meta { get; init; }   // e.g., paging info, version, links
    }
}
