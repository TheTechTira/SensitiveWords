using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SensitiveWords.Application.Abstractions.Caching;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Application.Common.Enums;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SensitiveWords.Infrastructure.Caching
{
    /// <summary>
    /// Default implementation that:
    /// - Reads the current <c>WordsVersion</c> + active words from the repository.
    /// - Caches a compiled regex keyed by <c>(wholeWord, version)</c>.
    /// - Uses a per-key <see cref="SemaphoreSlim"/> to prevent stampedes during rebuild.
    /// 
    /// Why this design:
    /// - <b>Versioned keys</b>: any data-changing repo method bumps WordsVersion → cache invalidates naturally.
    /// - <b>Short-lived scope</b>: resolve repo on demand without making this class depend on it directly.
    /// - <b>Compiled, culture-invariant, ignore-case</b>: fast, predictable matching across locales.
    /// </summary>
    public class WordsCache : IWordsCache
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;

        // Prevent multiple concurrent rebuilds for the same cache key (wholeWord, version).
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public WordsCache(IServiceScopeFactory scopeFactory, IMemoryCache cache)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        /// <inheritdoc/>
        public async Task<Regex> GetRegexAsync(bool wholeWord, CancellationToken ct)
        {
            // 1) Load the current version + active words through a short-lived scope.
            //    (Repository methods guarantee version bumps on mutation.)
            int version;
            List<(string Word, bool IsActive)> activeWords;

            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<ISensitiveWordRepository>();

                version = await repo.GetWordsVersionAsync(ct);

                var listActiveWordsResponse = await repo.ListActiveAsync(ct);
                if (listActiveWordsResponse.Status != EnumRepositoryResultStatus.Ok ||
                    listActiveWordsResponse.Data is null)
                {
                    // Return a "match-nothing" regex (consistent, safe fallback on errors/empty states).
                    return new Regex(@"(?!x)x");
                }

                activeWords = listActiveWordsResponse.Data
                    .Select(r => (r.Word, r.IsActive))
                    .Where(w => w.IsActive)
                    .ToList();
            }

            // 2) Build a stable cache key per version + matching mode (whole word vs. anywhere).
            var key = $"{(wholeWord ? "regex_whole" : "regex_any")}_v{version}";

            // 3) Fast path: return if already cached.
            if (_cache.TryGetValue<Regex>(key, out var cached))
                return cached!;

            // 4) Slow path: ensure only one builder per key runs concurrently (avoid stampede).
            var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                // Double-check cache after acquiring the gate.
                if (_cache.TryGetValue<Regex>(key, out cached))
                    return cached!;

                // 4.1) Build token patterns (phrase-aware, flexible whitespace, optional word boundaries).
                var tokens = activeWords
                    .Select(w => BuildTokenPattern(w.Word, wholeWord))
                    .Where(p => p.Length > 0)
                    .ToArray();

                var pattern = tokens.Length == 0
                    ? @"(?!x)x"                          // matches nothing
                    : $"(?:{string.Join("|", tokens)})"; // non-capturing alternation

                // NOTE: Consider adding RegexOptions.NonBacktracking (.NET 7+) if the token count grows large.
                var regex = new Regex(
                    pattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                // 4.2) Cache compiled regex. TTL is mostly a safeguard; version bumps are the real invalidator.
                _cache.Set(key, regex, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
                    Priority = CacheItemPriority.High
                    // Optional clean-up of per-key semaphore when this entry evicts:
                    // .RegisterPostEvictionCallback((k, _, __, ___) =>
                    // {
                    //     if (_locks.TryRemove(k.ToString()!, out var s)) s.Dispose();
                    // })
                });

                return regex;
            }
            finally
            {
                gate.Release();
                // Optional maintenance: if you enabled PostEviction cleanup above,
                // the semaphore is removed when the cache entry evicts.
            }
        }

        /// <summary>
        /// Builds a safe regex token for a single word/phrase.
        /// Behavior:
        /// - Escapes regex meta characters.
        /// - Collapses internal whitespace to a single space, then emits <c>\s+</c> to match flexible whitespace.
        /// - If <paramref name="wholeWord"/> is true, applies word-boundary lookarounds
        ///   only when the token begins/ends with <c>\w</c> (letters/digits/underscore).
        ///   This avoids false negatives for tokens that start/end with punctuation.
        /// </summary>
        /// <param name="raw">The raw word/phrase from the data store.</param>
        /// <param name="wholeWord">Whether to enforce word boundaries where appropriate.</param>
        /// <returns>A regex-safe token string, or empty string if input is blank.</returns>
        private static string BuildTokenPattern(string raw, bool wholeWord)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            // Normalize internal whitespace to a single space (then map to \s+ below).
            var normalized = Regex.Replace(raw.Trim(), @"\s+", " ");

            // Escape meta-chars; then replace spaces with \s+ for flexible matching across whitespace.
            var escaped = Regex.Escape(normalized).Replace(" ", @"\s+");

            if (!wholeWord) return escaped;

            // Smart word boundaries: only add lookarounds when token starts/ends with a "word" char.
            char first = normalized[0], last = normalized[^1];
            bool firstIsWord = char.IsLetterOrDigit(first) || first == '_';
            bool lastIsWord = char.IsLetterOrDigit(last) || last == '_';

            var prefix = firstIsWord ? @"(?<!\w)" : ""; // left edge if needed
            var suffix = lastIsWord ? @"(?!\w)" : ""; // right edge if needed

            return $"{prefix}{escaped}{suffix}";
        }
    }
}