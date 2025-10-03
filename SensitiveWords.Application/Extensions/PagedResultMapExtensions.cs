using SensitiveWords.Application.Common.Results;

namespace SensitiveWords.Application.Extensions
{
    public static class PagedResultMapExtensions
    {
        /// <summary>
        /// Map PagedResult<TIn> → PagedResult<TOut> while preserving paging metadata.
        /// </summary>
        public static PagedResult<TOut> Map<TIn, TOut>(
            this PagedResult<TIn> page,
            Func<TIn, TOut> mapper)
        {
            return new PagedResult<TOut>
            {
                Items = page.Items.Select(mapper).ToList(),
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = page.TotalCount
            };
        }
    }
}
