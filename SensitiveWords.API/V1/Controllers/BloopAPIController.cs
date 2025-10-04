using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Application.Common.Responses;
using SensitiveWords.Domain.Dtos;

namespace SensitiveWords.API.V1.Controllers
{
    /// <summary>
    /// Message "blooping" (masking) endpoint (External Use API).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/messages")]
    [Audience(AudienceAttribute.External)]
    [Produces("application/json")]
    [Tags("Bloop Messages")]
    [EnableRateLimiting("BloopPerHour")]// Enable rate limiting policy at the controller level (can be overridden per action)
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
