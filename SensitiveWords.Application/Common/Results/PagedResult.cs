namespace SensitiveWords.Application.Common.Results
{
    /// <summary>
    /// Common interface for paginated results.
    /// Provides information about the current page, page size,
    /// total count of items, and navigation helpers (HasNext, HasPrevious).
    /// </summary>
    public interface IPagedResult
    {
        /// <summary>
        /// The current page number (1-based).
        /// </summary>
        int Page { get; }

        /// <summary>
        /// The number of items per page.
        /// </summary>
        int PageSize { get; }

        /// <summary>
        /// The total number of items available (across all pages).
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// The total number of pages given <see cref="TotalCount"/> and <see cref="PageSize"/>.
        /// </summary>
        int TotalPages { get; }

        /// <summary>
        /// True if there is a page after the current one.
        /// </summary>
        bool HasNext { get; }

        /// <summary>
        /// True if there is a page before the current one.
        /// </summary>
        bool HasPrevious { get; }
    }

    /// <summary>
    /// Concrete implementation of <see cref="IPagedResult"/> holding the
    /// actual list of items along with pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of items being paged.</typeparam>
    public class PagedResult<T> : IPagedResult
    {
        /// <summary>
        /// The items for the current page.
        /// </summary>
        public required IReadOnlyList<T> Items { get; init; }

        /// <inheritdoc/>
        public required int Page { get; init; }

        /// <inheritdoc/>
        public required int PageSize { get; init; }

        /// <inheritdoc/>
        public required int TotalCount { get; init; }

        /// <inheritdoc/>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <inheritdoc/>
        public bool HasNext => Page < TotalPages;

        /// <inheritdoc/>
        public bool HasPrevious => Page > 1;
    }
}