using SensitiveWords.Application.Abstractions.Caching;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Domain.Dtos;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SensitiveWords.Application.Services
{
    /// <summary>
    /// Applies the sensitive-word regex to a message and returns a masked ("blooped") result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The regex comes from <see cref="IWordsCache"/> which compiles it (case-insensitive,
    /// culture-invariant) and versions it via metadata so callers don’t rebuild patterns unnecessarily.
    /// </para>
    /// <para>
    /// Masking strategy: each match is replaced with a string of <c>'*'</c> characters
    /// equal to the match length (simple, reversible-proof, preserves layout).
    /// If you need a different policy (fixed length, partial masking, custom char),
    /// consider introducing options (e.g., <c>BloopOptions</c>).
    /// </para>
    /// <para>
    /// Performance note: <see cref="Regex.Replace(string, MatchEvaluator)"/> executes synchronously.
    /// We only await the regex retrieval. <see cref="ElapsedMs"/> measures server-side work here
    /// (cache fetch + replace), not network latency.
    /// </para>
    /// <para>
    /// Cancellation: honored while fetching the regex; the replace itself is not cancelable.
    /// If you need mid-replace cancellation for very large inputs, you’d need chunked processing.
    /// </para>
    /// </remarks>
    public class BloopService : IBloopService
    {
        private readonly IWordsCache _cache;

        public BloopService(IWordsCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Masks all sensitive-word matches in <paramref name="request"/> using the compiled regex
        /// and returns the original text, masked text, match count, and elapsed time (ms).
        /// </summary>
        /// <param name="request">
        /// Input message and matching mode (<see cref="BloopRequestDto.WholeWord"/> decides whether
        /// word boundaries are enforced by the regex).
        /// </param>
        /// <param name="ct">Cancellation token (applies to regex retrieval only).</param>
        /// <returns>A <see cref="BloopResponseDto"/> with the original, blooped text, match count, and runtime.</returns>
        /// <remarks>
        /// Assumptions:
        /// <list type="bullet">
        ///   <item><description><c>request.Message</c> is non-null (controller/model validation should enforce).</description></item>
        ///   <item><description>Regex is pre-escaped/constructed safely by <see cref="IWordsCache"/>.</description></item>
        ///   <item><description>Match count increments once per regex match (no overlap counting).</description></item>
        /// </list>
        /// </remarks>
        public async Task<BloopResponseDto> BloopAsync(BloopRequestDto request, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            // Get (or build) the compiled regex for current version and whole-word mode.
            var regex = await _cache.GetRegexAsync(request.WholeWord, ct);

            // Replace with same-length asterisks and count matches.
            int matches = 0;
            string Evaluator(Match m)
            {
                matches++;
                return new string('*', m.Length);
            }

            var blooped = regex.Replace(request.Message, new MatchEvaluator(Evaluator));

            sw.Stop();
            return new BloopResponseDto(
                original: request.Message,
                blooped: blooped,
                matches: matches,
                elapsedMs: sw.ElapsedMilliseconds);
        }
    }
}
