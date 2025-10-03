using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Application.Common.Responses;
using SensitiveWords.Domain.Dtos;

namespace SensitiveWords.API.Controllers
{
    /// <summary>
    /// EXTERNAL API — Message “blooping” (masking) endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Public-facing endpoint to scan a message and mask sensitive words/phrases. This route is intended
    /// for first-party clients (web/app/services). It returns a <see cref="BloopResponseDto"/> directly
    /// (no envelope), to keep the contract small and simple for external callers.
    /// </para>
    /// <para>
    /// 🔐 <b>Security:</b> Protect this route with your API gateway and authentication (e.g., JWT/OIDC)
    /// before exposing publicly. Apply rate-limiting (configured via <c>BloopPerHour</c> policy) to deter abuse.
    /// </para>
    /// <para>
    /// 🧠 <b>Matching mode:</b> The request’s <c>WholeWord</c> flag controls boundary handling. When true,
    /// the underlying regex uses word edges where appropriate; when false, matches substrings anywhere.
    /// </para>
    /// <para>
    /// 📄 <b>Example request</b>:
    /// <code>
    /// POST /api/v1.0/messages/bloop
    /// {
    ///   "message": "Please don't DROP TABLE users;",
    ///   "wholeWord": true
    /// }
    /// </code>
    /// <b>Example response</b>:
    /// <code>
    /// {
    ///   "original": "Please don't DROP TABLE users;",
    ///   "blooped":  "Please don't **** ***** users;",
    ///   "matches":  2,
    ///   "elapsedMs": 3
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Notes:
    /// <list type="bullet">
    ///   <item><description>Validation errors (e.g., missing <c>message</c>) return <c>400</c> with your standard error body.</description></item>
    ///   <item><description>When rate limit is exceeded, returns <c>429</c> (per <c>BloopPerHour</c> policy).</description></item>
    ///   <item><description>Server faults return <c>500</c> with a trace id.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/messages")]
    [Audience(AudienceAttribute.External)]
    [Produces("application/json")]
    [Tags("Bloop")]
    public class BloopAPIController : ControllerBase
    {
        private readonly IBloopService _svc;
        public BloopAPIController(IBloopService svc) => _svc = svc;

        /// <summary>
        /// Stars-out (masks) sensitive words/phrases in a message.
        /// </summary>
        /// <remarks>
        /// Uses a compiled, cached regex sourced from the internal words repository. Each match is replaced by
        /// a string of asterisks of equal length. If you need a different masking policy, change it in <c>BloopService</c>.
        /// </remarks>
        /// <param name="req">Message to scan and the matching mode (whole-word vs substring).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Masked result with match count and elapsed processing time.</response>
        /// <response code="400">Validation failure (e.g., missing or empty <c>message</c>).</response>
        /// <response code="429">Too many requests (rate limit exceeded).</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpPost("bloop")]
        [EnableRateLimiting("BloopPerHour")] // configure in Program.cs: one token bucket per client identity
        [ProducesResponseType(typeof(BloopResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Bloop([FromBody] BloopRequestDto req, CancellationToken ct = default)
            => Ok(await _svc.BloopAsync(req, ct));
    }
}
