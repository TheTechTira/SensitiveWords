using Microsoft.AspNetCore.Mvc;
using SensitiveWords.Application.Common.Enums;
using SensitiveWords.Application.Common.Responses;
using SensitiveWords.Application.Common.Results;

namespace SensitiveWords.API.Extensions
{
    /// <summary>
    /// Controller helpers to convert <see cref="ServiceResult{T}"/> into typed HTTP responses.
    ///
    /// Why:
    /// - Centralizes the mapping from service-layer outcomes to API HTTP shapes.
    /// - Gives callers optional hooks to customize OK responses without duplicating switch logic:
    ///   <list type="bullet">
    ///     <item><description><paramref name="onOk"/> lets the caller fully control the 200 response (e.g., CreatedAtAction).</description></item>
    ///     <item><description><paramref name="writeOkHeaders"/> for paging/ratelimit headers.</description></item>
    ///     <item><description><paramref name="buildOkMeta"/> to attach small metadata (paging, timers) into the default body.</description></item>
    ///   </list>
    ///
    /// Notes:
    /// - Error responses use a ProblemDetails-like <c>ErrorResponse</c> (traceId + errorCode).
    /// - We stick to 400 for invalid, 404 not found, 409 conflict, and 500 for unknown errors.
    ///   If you prefer 422 for validation, change the status + Type link accordingly.
    /// </summary>
    public static class ControllerServiceResultExtensions
    {
        /// <summary>
        /// Converts a <see cref="ServiceResult{T}"/> into an <see cref="IActionResult"/>.
        /// </summary>
        /// <typeparam name="T">Payload type of the service result.</typeparam>
        /// <param name="result">Service-layer outcome to map.</param>
        /// <param name="controller">Calling controller (used for HttpContext + helper methods).</param>
        /// <param name="onOk">
        /// Optional factory that builds a custom 200-series response (e.g., <c>CreatedAtAction</c> or custom envelope).
        /// If provided, it completely replaces the default SuccessResponse body.
        /// </param>
        /// <param name="writeOkHeaders">
        /// Optional action to write headers only for successful responses (e.g., <c>X-Total-Count</c>, link headers).
        /// </param>
        /// <param name="buildOkMeta">
        /// Optional builder that returns a small, JSON-serializable metadata object to include
        /// in the default success envelope (e.g., paging { page, pageSize, total }).
        /// Ignored when <paramref name="onOk"/> is provided.
        /// </param>
        public static IActionResult ToActionResult<T>(
            this ServiceResult<T> result,
            ControllerBase controller,
            Func<T?, IActionResult>? onOk = null,
            Action<ControllerBase, T?>? writeOkHeaders = null,
            Func<T?, object?>? buildOkMeta = null)
        {
            var traceId = controller.HttpContext.TraceIdentifier;

            switch (result.Status)
            {
                case EnumServiceResultStatus.Ok:
                    {
                        // Give callers a hook to add headers (e.g., pagination/ratelimit)
                        writeOkHeaders?.Invoke(controller, result.Data);

                        // If caller wants full control of the 200 response, defer to them.
                        if (onOk is not null)
                            return onOk(result.Data);

                        // Default 200 OK envelope
                        var body = new SuccessResponse<T>
                        {
                            Data = result.Data,
                            Message = result.Message,
                            TraceId = traceId,
                            Meta = buildOkMeta?.Invoke(result.Data)
                        };
                        return controller.Ok(body);
                    }

                case EnumServiceResultStatus.NotFound:
                    return controller.NotFound(new ErrorResponse
                    {
                        Type = "https://httpstatuses.com/404",
                        Title = "Resource not found",
                        Status = StatusCodes.Status404NotFound,
                        TraceId = traceId,
                        Detail = result.Message,
                        ErrorCode = result.ErrorCode
                    });

                case EnumServiceResultStatus.Conflict:
                    return controller.Conflict(new ErrorResponse
                    {
                        Type = "https://httpstatuses.com/409",
                        Title = "Conflict",
                        Status = StatusCodes.Status409Conflict,
                        TraceId = traceId,
                        Detail = result.Message,
                        ErrorCode = result.ErrorCode
                    });

                case EnumServiceResultStatus.Invalid:
                    return controller.BadRequest(new ErrorResponse
                    {
                        Type = "https://httpstatuses.com/400",
                        Title = "Invalid request",
                        Status = StatusCodes.Status400BadRequest,
                        TraceId = traceId,
                        Detail = result.Message,
                        ErrorCode = result.ErrorCode
                    });

                case EnumServiceResultStatus.Error:
                default:
                    return controller.StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                    {
                        Type = "https://httpstatuses.com/500",
                        Title = "Internal server error",
                        Status = StatusCodes.Status500InternalServerError,
                        TraceId = traceId,
                        Detail = result.Message,
                        ErrorCode = result.ErrorCode
                    });
            }
        }

        /// <summary>
        /// If <paramref name="result"/> is OK, returns a new OK result with a custom success message.
        /// For non-OK statuses, returns the original result unchanged.
        /// 
        /// Caveat:
        /// - This recreates an OK result via <c>ServiceResult&lt;T&gt;.Ok(data, message)</c>.
        ///   If your OK result carries additional metadata (e.g., Affected, CorrelationId),
        ///   and the factory doesn't accept them, they won't be preserved. Consider adding
        ///   richer <c>Ok(...)</c> overloads if needed.
        /// </summary>
        public static ServiceResult<T> WithMessage<T>(this ServiceResult<T> result, string messageWhenOk)
            => result.Status == EnumServiceResultStatus.Ok
               ? ServiceResult<T>.Ok(result.Data, messageWhenOk)
               : result; // leave non-OK untouched
    }
}
