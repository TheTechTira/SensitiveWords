using System.Text.Json.Serialization;

namespace SensitiveWords.Domain.Dtos
{
    /// <summary>
    /// Response returned after scanning and masking a message ("blooping").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Original</b> is the exact input text received. <b>Blooped</b> is the output after
    /// sensitive terms have been masked or transformed by the service’s policy.
    /// </para>
    /// <para>
    /// <b>Matches</b> is the total number of matches found (and typically masked). Depending on
    /// the implementation, this may count overlapping matches or phrase matches once—check the
    /// service’s masking rules if exact semantics matter.
    /// </para>
    /// <para>
    /// <b>ElapsedMs</b> is the server-side processing time in milliseconds for the scan/mask step
    /// (usually measured with <c>Stopwatch</c>); it excludes network latency and client time.
    /// Use it as a rough performance indicator, not an SLA.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// {
    ///   "original": "Please don't DROP TABLE users;",
    ///   "blooped":  "Please don't ████ █████ users;",
    ///   "matches":  2,
    ///   "elapsedMs": 3
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Note: If callers later need more detail (e.g., which words were matched or their positions),
    /// consider adding an optional array (e.g., <c>hits</c>) to this DTO to preserve compatibility.
    /// </para>
    /// </remarks>
    public class BloopResponseDto
    {
        /// <summary>
        /// Creates a new response with the original text, the masked result, the match count,
        /// and the server-side processing time in milliseconds.
        /// </summary>
        /// <param name="original">The exact input text that was scanned.</param>
        /// <param name="blooped">The output text after masking/transformation.</param>
        /// <param name="matches">Total number of matches found (and usually masked).</param>
        /// <param name="elapsedMs">Processing time on the server, in milliseconds.</param>
        public BloopResponseDto(string original, string blooped, int matches, long elapsedMs)
        {
            Original = original;
            Blooped = blooped;
            Matches = matches;
            ElapsedMs = elapsedMs;
        }

        /// <summary>
        /// The exact input text that was scanned. Returned for caller convenience/debugging.
        /// </summary>
        [JsonPropertyName("original")]
        public string Original { get; set; }

        /// <summary>
        /// The masked or transformed text produced by the service.
        /// </summary>
        [JsonPropertyName("blooped")]
        public string Blooped { get; set; }

        /// <summary>
        /// Total number of matches discovered during the scan (implementation-defined).
        /// </summary>
        [JsonPropertyName("matches")]
        public int Matches { get; set; }

        /// <summary>
        /// Server-side processing time in milliseconds for the scan/mask operation.
        /// </summary>
        [JsonPropertyName("elapsedMs")]
        public long ElapsedMs { get; set; }
    }
}
