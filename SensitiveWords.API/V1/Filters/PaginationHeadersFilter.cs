using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using SensitiveWords.Application.Common.Results;
using System.Web;

namespace SensitiveWords.API.Filters
{
    /// <summary>
    /// Writes pagination headers for responses that carry an <see cref="IPagedResult"/>.
    ///
    /// What it does:
    /// - Adds these response headers (lowercase bools for consistency):
    ///   X-Page, X-PageSize, X-TotalCount, X-TotalPages, X-HasNext, X-HasPrev
    /// - Works when the action returns either:
    ///   1) the <see cref="IPagedResult"/> directly (e.g., Ok(paged)), or
    ///   2) a success envelope where the <c>Data</c> property is an <see cref="IPagedResult"/>
    ///      (e.g., Ok(new SuccessResponse{ Data = paged }))
    ///
    /// Why a filter:
    /// - Centralizes header emission so controllers don’t repeat this logic.
    /// - Runs late enough to inspect the final object but before headers are sent.
    ///
    /// Notes:
    /// - If you always return a custom envelope, this filter still works by peeking into "Data".
    /// - Consider also adding Link headers (RFC 5988) for HATEOAS; sample shown below.
    /// - If you already write headers elsewhere (e.g., in an extension), avoid double-writing.
    /// </summary>
    public class PaginationHeadersFilter : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not ObjectResult objectResult || objectResult.Value is null)
                return;

            if (!TryGetPagedPayload(objectResult.Value, out var p))
                return;

            var headers = context.HttpContext.Response.Headers;

            headers["X-Page"] = p.Page.ToString();
            headers["X-PageSize"] = p.PageSize.ToString();
            headers["X-TotalCount"] = p.TotalCount.ToString();
            headers["X-TotalPages"] = p.TotalPages.ToString();
            headers["X-HasNext"] = p.HasNext.ToString().ToLowerInvariant();
            headers["X-HasPrev"] = p.HasPrevious.ToString().ToLowerInvariant();

            // Optional: emit RFC 5988 Link header (first, prev, next, last)
            // headers["Link"] = BuildLinkHeader(context, p);
        }

        public void OnResultExecuted(ResultExecutedContext context) { }

        /// <summary>
        /// Tries to extract an <see cref="IPagedResult"/> from the returned value.
        /// Handles either a direct IPagedResult or an envelope with a "Data" property
        /// that is an IPagedResult (e.g., SuccessResponse{ Data = paged }).
        /// </summary>
        private static bool TryGetPagedPayload(object value, out IPagedResult paged)
        {
            // Direct case
            if (value is IPagedResult direct)
            {
                paged = direct;
                return true;
            }

            // Envelope case: look for a property named "Data" and check if it's IPagedResult
            var dataProp = value.GetType().GetProperty("Data");
            if (dataProp is not null)
            {
                var data = dataProp.GetValue(value);
                if (data is IPagedResult inner)
                {
                    paged = inner;
                    return true;
                }
            }

            paged = default!;
            return false;
        }

        /// <summary>
        /// (Optional) Builds a Link header with first/prev/next/last relations.
        /// Preserves existing query parameters and replaces/sets "page" &amp; "pageSize".
        /// </summary>
        private static StringValues BuildLinkHeader(ResultExecutingContext ctx, IPagedResult p)
        {
            var req = ctx.HttpContext.Request;
            var baseUri = $"{req.Scheme}://{req.Host}{req.Path}";

            string BuildUrl(int page, int pageSize)
            {
                var q = HttpUtility.ParseQueryString(req.QueryString.Value ?? string.Empty);
                q.Set("page", page.ToString());
                q.Set("pageSize", pageSize.ToString());
                return $"{baseUri}?{q}";
            }

            var links = new List<string>
            {
                $"<{BuildUrl(1, p.PageSize)}>; rel=\"first\"",
                $"<{BuildUrl(p.TotalPages == 0 ? 1 : p.TotalPages, p.PageSize)}>; rel=\"last\""
            };

            if (p.HasPrevious) links.Add($"<{BuildUrl(p.Page - 1, p.PageSize)}>; rel=\"prev\"");
            if (p.HasNext) links.Add($"<{BuildUrl(p.Page + 1, p.PageSize)}>; rel=\"next\"");

            return new StringValues(string.Join(", ", links));
        }
    }
}
