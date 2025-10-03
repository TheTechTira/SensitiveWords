using System.Text.RegularExpressions;

namespace SensitiveWords.Application.Abstractions.Caching
{
    /// <summary>
    /// Builds and caches a compiled <see cref="Regex"/> that matches configured sensitive words.
    /// 
    /// - The regex is rebuilt whenever the repository's "WordsVersion" changes.
    /// - Two cache variants exist: whole-word and substring (controlled by <paramref name="wholeWord"/>).
    /// - Callers should re-fetch the regex when they need the latest (e.g., per request or per interval).
    /// </summary>
    public interface IWordsCache
    {
        /// <summary>
        /// Returns a compiled regex that matches the active sensitive words.
        /// The returned instance is cached (per version &amp; mode) for fast reuse.
        /// </summary>
        /// <param name="wholeWord">
        /// If true, enforce word boundaries where appropriate (see <see cref="BuildTokenPattern"/>).
        /// If false, match anywhere within the text.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        Task<Regex> GetRegexAsync(bool wholeWord, CancellationToken ct);
    }
}
