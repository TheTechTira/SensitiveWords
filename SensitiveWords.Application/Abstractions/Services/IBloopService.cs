using SensitiveWords.Domain.Dtos;

namespace SensitiveWords.Application.Abstractions.Services
{
    /// <summary>
    /// Service contract for masking ("blooping") sensitive words in a message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations typically obtain a compiled, versioned regex from <c>IWordsCache</c>,
    /// apply it to the input message, and return a response containing the original text,
    /// the masked result, the total match count, and elapsed processing time.
    /// </para>
    /// <para>
    /// Thread-safety: Implementations should be stateless and safe to use across requests.
    /// Regex compilation/caching concerns belong in <c>IWordsCache</c>.
    /// </para>
    /// </remarks>
    public interface IBloopService
    {
        /// <summary>
        /// Masks all sensitive-word matches in the given request and returns details of the operation.
        /// </summary>
        /// <param name="request">
        /// Input containing the <c>Message</c> to scan and the matching mode
        /// (<c>WholeWord</c> controls boundary behavior in the underlying regex).
        /// </param>
        /// <param name="ct">
        /// Cancellation token (observed while retrieving/constructing the regex; the replace operation
        /// itself is typically synchronous and may not observe cancellation).
        /// </param>
        /// <returns>
        /// A <see cref="BloopResponseDto"/> containing the original text, the masked text,
        /// the number of matches found, and the server-side elapsed time in milliseconds.
        /// </returns>
        /// <remarks>
        /// <list type="bullet">
        ///   <item><description>May throw <see cref="OperationCanceledException"/> if <paramref name="ct"/> is canceled.</description></item>
        ///   <item><description>Implementations should treat <c>request.Message</c> as required (validate in the API layer).</description></item>
        ///   <item><description>Matching semantics (whole word vs substring, whitespace handling, etc.) are dictated by the regex supplied by <c>IWordsCache</c>.</description></item>
        /// </list>
        /// </remarks>
        Task<BloopResponseDto> BloopAsync(BloopRequestDto request, CancellationToken ct);
    }
}
