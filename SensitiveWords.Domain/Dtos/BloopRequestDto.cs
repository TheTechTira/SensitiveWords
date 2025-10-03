namespace SensitiveWords.Domain.Dtos
{
    /// <summary>
    /// Request payload used when checking a text message against the sensitive-words list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Message</b> is the raw text to scan. <b>WholeWord</b> controls how the regex is built:
    /// when true, the service will prefer word-boundary matches (e.g., matches <c>"drop"</c> as a word,
    /// but not inside <c>"backdrop"</c>); when false, it will match anywhere in the string.
    /// </para>
    /// <para>
    /// Internally this typically flows into <c>IWordsCache.GetRegexAsync(WholeWord)</c>, then the compiled
    /// regex is applied to <c>Message</c>.
    /// </para>
    /// <para>
    /// <b>Example</b>:
    /// <code>
    /// {
    ///   "message": "Please don't DROP TABLE users;",
    ///   "wholeWord": true
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Tip: If you want model-validation (400 on missing/empty message), add
    /// <c>[Required]</c> / <c>[StringLength]</c> attributes to <see cref="Message"/> in the future.
    /// We avoided adding them here to keep current behavior unchanged.
    /// </para>
    /// </remarks>
    public class BloopRequestDto
    {
        /// <summary>
        /// Creates a new request with the text to scan and the matching mode.
        /// </summary>
        /// <param name="message">The raw text to check against the sensitive-words regex.</param>
        /// <param name="wholeWord">
        /// If true, prefer whole-word/phrase matches (word boundaries where appropriate);
        /// if false, allow substring matches. Defaults to true.
        /// </param>
        public BloopRequestDto(string message, bool wholeWord)
        {
            Message = message;
            WholeWord = wholeWord;
        }

        /// <summary>
        /// The input text to be scanned for sensitive words.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// When true, applies word boundaries where sensible (whole-word match);
        /// when false, matches anywhere within the text. Defaults to true.
        /// </summary>
        public bool WholeWord { get; set; } = true;
    }
}
