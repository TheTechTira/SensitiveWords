using System.Diagnostics;

namespace SensitiveWords.Domain.ValueObjects
{
    /// <summary>
    /// Mutable counter for a single rate-limit key (e.g., client IP, API key).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This object is stored per key and tracks:
    /// <list type="bullet">
    ///   <item><description><see cref="Count"/> — how many requests have been made in the current window.</description></item>
    ///   <item><description><see cref="ResetAtUtc"/> — when the current window ends (UTC).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> multiple requests may update the same instance concurrently.
    /// Always increment using <c>Interlocked.Increment(ref state.Count)</c> and
    /// reset the window using an atomic compare-and-swap pattern (or guard with a lock) to avoid races.
    /// </para>
    /// <para>
    /// <b>Usage pattern:</b>
    /// <code>
    /// var now = DateTimeOffset.UtcNow;
    /// if (now >= state.ResetAtUtc)
    /// {
    ///     // window elapsed: reset counter and move window
    ///     state.Count = 0;                      // optionally Interlocked.Exchange(ref state.Count, 0);
    ///     state.ResetAtUtc = now.Add(window);   // e.g., 1 hour/1 minute
    /// }
    /// var newCount = Interlocked.Increment(ref state.Count);
    /// var allowed = newCount <= limit;
    /// </code>
    /// </para>
    /// <para>
    /// <b>Distributed stores:</b> if this state lives in Redis/SQL/etc., do the increment/reset as a single
    /// atomic operation (e.g., Lua script in Redis, UPDATE with WHERE + rowversion in SQL) to prevent lost updates.
    /// </para>
    /// </remarks>
    [DebuggerDisplay("Count = {Count}, ResetAtUtc = {ResetAtUtc:u}")]
    public class RateLimitCounterState
    {
        /// <summary>
        /// Number of requests observed in the current window.
        /// Increment with <see cref="System.Threading.Interlocked.Increment(ref long)"/> to stay thread-safe.
        /// </summary>
        public long Count;  // intentionally a field for Interlocked.* ops

        /// <summary>
        /// UTC timestamp when the current window ends; on/after this time the counter should be reset and a new window started.
        /// </summary>
        public DateTimeOffset ResetAtUtc;
    }
}
