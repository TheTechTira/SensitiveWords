using Microsoft.AspNetCore.Mvc;
using SensitiveWords.Application.Common.Responses;

namespace SensitiveWords.API.Extensions
{
    /// <summary>
    /// Configures a consistent API response for model validation failures produced by
    /// ASP.NET Core's <c>[ApiController]</c> automatic model state validation.
    ///
    /// Why:
    /// - Ensures a stable, contract-first error shape for clients (our <see cref="ErrorResponse"/>).
    /// - Adds a <c>TraceId</c> so support can correlate client reports with server logs.
    /// - Avoids leaking framework-default <c>ProblemDetails</c> shapes that may change.
    /// </summary>
    public static class ApiBehaviorExtensions
    {
        /// <summary>
        /// Registers a custom <see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/> that
        /// returns an <see cref="ErrorResponse"/> with field-level errors when model validation fails.
        ///
        /// Usage (in Program.cs):
        /// <code>
        /// builder.Services.UseStandardModelValidation();
        /// </code>
        ///
        /// Notes:
        /// - This kicks in only for controllers annotated with <c>[ApiController]</c>.
        /// - We return HTTP 400 (Bad Request). Some teams prefer 422 (Unprocessable Entity)
        ///   to distinguish syntactic vs semantic errors; switch the status if that aligns better.
        /// - Keys in <c>Errors</c> reflect ModelState field paths (e.g., "items[0].name").
        /// </summary>
        public static void UseStandardModelValidation(this IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(opts =>
            {
                opts.InvalidModelStateResponseFactory = ctx =>
                {
                    var traceId = ctx.HttpContext.TraceIdentifier;

                    // Collect field-level error messages from ModelState
                    var errors = ctx.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    var resp = new ErrorResponse
                    {
                        Type = "https://httpstatuses.com/400",        // RFC 7807-style "type" (docs URL preferred)
                        Title = "Validation failed",                  // short, human-readable summary
                        Status = StatusCodes.Status400BadRequest,     // choose 422 if you prefer
                        TraceId = traceId,
                        ErrorCode = "validation_failed",              // stable machine-readable code
                        Errors = errors
                    };

                    return new BadRequestObjectResult(resp);
                };
            });
        }
    }
}
