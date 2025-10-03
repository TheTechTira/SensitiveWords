namespace SensitiveWords.Application.Common.Options
{
    /// <summary>
    /// Options for policy-level restrictions around sensitive words.
    ///
    /// Purpose:
    /// - Provides a configuration-driven list of words that the application
    ///   should not allow to be created/used (e.g., enforced in the service layer).
    ///
    /// Design notes for the next dev:
    /// - We keep this as a plain POCO bound from configuration (IOptions pattern).
    /// - Defaults to an empty array (never null) to simplify consumers.
    /// - Case sensitivity and normalization are NOT handled here; the service
    ///   constructs a case-insensitive <see cref="HashSet{T}"/> and performs
    ///   any normalization (e.g., Trim) before checks.
    /// - Duplicates in configuration are harmless; consumers typically
    ///   de-duplicate via a set.
    /// </summary>
    public class SensitiveWordPolicyOptions
    {
        /// <summary>
        /// Words that are disallowed by policy.
        /// Expected to be a small list coming from configuration.
        /// Consumers may interpret this list case-insensitively.
        /// Defaults to <see cref="Array.Empty{T}"/> so it's never null.
        /// </summary>
        public string[] BlockedWords { get; set; } = Array.Empty<string>();
    }
}
