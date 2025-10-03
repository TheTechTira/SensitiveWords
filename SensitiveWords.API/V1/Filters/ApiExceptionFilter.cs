using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SensitiveWords.Application.Common.Responses;
using System.Diagnostics;

namespace SensitiveWords.API.V1.Filters
{
    /// <summary>
    /// Global MVC exception filter that converts unhandled exceptions into a consistent error envelope.
    ///
    /// Why a filter (vs middleware):
    /// - Runs within MVC's pipeline so you can return typed results (e.g., JSON) using your API contract
    ///   (<see cref="ErrorResponse"/> with <c>TraceId</c>, <c>ErrorCode</c>).
    /// - Keeps controllers lean; you don't need try/catch in each action.
    ///
    /// Notes:
    /// - In Development we surface the exception message in <see cref="ErrorResponse.Detail"/> for convenience;
    ///   in non-Development we suppress details to avoid leaking internals (OWASP friendly).
    /// - We log the full exception with <see cref="ILogger"/> so ops has complete context.
    /// - Consider the built-in exception handling middleware (.NET 7/8) if you prefer a middleware-first approach.
    /// </summary>
    public class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;
        private readonly IHostEnvironment _env;

        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Converts any unhandled exception into a 500 response using <see cref="ErrorResponse"/>.
        /// Special-cases operation cancellations so we don't treat them as server faults.
        /// </summary>
        public void OnException(ExceptionContext context)
        {
            var ex = context.Exception;

            // 1) If the request was canceled, don't report a server error.
            //    Let the framework produce its default response or translate to 499 if you prefer.
            if (ex is OperationCanceledException or TaskCanceledException)
            {
                _logger.LogInformation(ex, "Request was canceled by the client.");
                // Option A: let it bubble (do nothing) and let the host handle it.
                // Option B: explicitly mark as handled with 499 (Client Closed Request) if that's your policy:
                // context.Result = new StatusCodeResult(StatusCodes.Status499ClientClosedRequest);
                // context.ExceptionHandled = true;
                return;
            }

            // 2) Log the exception with stack trace for ops/telemetry.
            _logger.LogError(ex, "Unhandled exception");

            // 3) Correlate with tracing; prefer Activity Id when present, else HttpContext.TraceIdentifier.
            var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            // 4) Shape the client response. Hide details outside Development.
            var resp = new ErrorResponse
            {
                Type = "https://httpstatuses.com/500",                // RFC7807-style problem type
                Title = "Internal Server Error",                      // short human summary
                Status = StatusCodes.Status500InternalServerError,
                TraceId = traceId,
                Detail = _env.IsDevelopment() ? ex.Message : null,    // don't leak details in prod
                ErrorCode = "unhandled_exception"                     // stable machine-readable code
            };

            context.Result = new ObjectResult(resp) { StatusCode = resp.Status };
            context.ExceptionHandled = true;
        }
    }
}
