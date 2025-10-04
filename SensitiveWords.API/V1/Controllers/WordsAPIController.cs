using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SensitiveWords.API.V1.Contracts;
using SensitiveWords.API.V1.Extensions;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Application.Common.Responses;
using SensitiveWords.Application.Common.Results;
using SensitiveWords.Domain.Dtos;
using Swashbuckle.AspNetCore.Annotations;

namespace SensitiveWords.API.V1.Controllers
{
    /// <summary>
    /// Sensitive words management (internal only). Do not expose publicly.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/sensitive-words")]
    [Audience(AudienceAttribute.Internal)]
    [Produces("application/json")]
    [Tags("Sensitive Words")]
    // NOTE: For public builds, uncomment to hide these endpoints from Swagger:
    // [ApiExplorerSettings(IgnoreApi = true)]
    public class WordsAPIController : ControllerBase
    {
        private readonly ISensitiveWordService _svc;
        public WordsAPIController(ISensitiveWordService svc) => _svc = svc;

        /// <summary>
        /// List sensitive words (paged).
        /// </summary>
        /// <remarks>Heyto</remarks>
        /// <param name="page">1-based page number (default 1).</param>
        /// <param name="pageSize">Page size (default 50; server clamps, max 200).</param>
        /// <param name="search">Optional case-insensitive LIKE filter on <c>word</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Success envelope containing a paged result.</response>
        /// <response code="500">Unexpected error.</response>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessResponse<PagedResult<SensitiveWordDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
            => (await _svc.ListAsync(page, pageSize, search, ct)).ToActionResult(this);

        /// <summary>
        /// (Demo) List words and write pagination headers manually.
        /// </summary>
        /// <remarks>
        /// Returns the <b>raw</b> paged payload (no success envelope) and sets headers:
        /// <c>X-Page</c>, <c>X-PageSize</c>, <c>X-TotalCount</c>, <c>X-TotalPages</c>, <c>X-HasNext</c>, <c>X-HasPrev</c>.
        /// In production prefer a global filter (e.g., <c>PaginationHeadersFilter</c>) over per-action header logic.
        /// </remarks>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="search">Optional search.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Raw <c>PagedResult&lt;SensitiveWordDto&gt;</c> with pagination headers.</response>
        [HttpGet("with-manual-headers")]
        [ProducesResponseType(typeof(PagedResult<SensitiveWordDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List_ManualHeaders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
        {
            var res = await _svc.ListAsync(page, pageSize, search, ct);

            return res.ToActionResult(
                this,
                onOk: data => Ok(data), // return the raw paged payload (for demo)
                writeOkHeaders: (c, data) =>
                {
                    if (data is PagedResult<SensitiveWordDto> p)
                    {
                        c.Response.Headers["X-Page"] = p.Page.ToString();
                        c.Response.Headers["X-PageSize"] = p.PageSize.ToString();
                        c.Response.Headers["X-TotalCount"] = p.TotalCount.ToString();
                        c.Response.Headers["X-TotalPages"] = p.TotalPages.ToString();
                        c.Response.Headers["X-HasNext"] = p.HasNext.ToString().ToLowerInvariant();
                        c.Response.Headers["X-HasPrev"] = p.HasPrevious.ToString().ToLowerInvariant();

                        c.Response.Headers["Custom"] = "We can header what we want!";
                        // If these headers must be readable by browsers, expose them via CORS:
                        // Response.Headers["Access-Control-Expose-Headers"] = "X-Page,X-PageSize,X-TotalCount,X-TotalPages,X-HasNext,X-HasPrev";
                    }
                },
                buildOkMeta: data => data is IPagedResult p
                    ? new { p.Page, p.PageSize, p.TotalCount, p.TotalPages, p.HasNext, p.HasPrevious }
                    : null
            );
        }

        /// <summary>
        /// Get a single sensitive word by Id.
        /// </summary>
        /// <param name="id">Word identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="200">Success envelope containing the word.</response>
        /// <response code="404">Not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(SuccessResponse<SensitiveWordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromRoute] int id, CancellationToken ct = default)
            => (await _svc.GetAsync(id, ct)).ToActionResult(this);

        /// <summary>
        /// Create a new sensitive word.
        /// </summary>
        /// <remarks>
        /// Requires non-empty <c>word</c>. On success returns <c>201 Created</c> with the new <c>id</c>.
        /// </remarks>
        /// <param name="req">Create payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">Created (success envelope contains the new <c>id</c>).</response>
        /// <response code="400">Validation or policy failure.</response>
        /// <response code="409">Duplicate word conflict.</response>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessResponse<object>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateSensitiveWordRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Word))
                return ValidationProblem("Word is required.");

            var res = await _svc.CreateAsync(req.Word, req.IsActive, ct);

            // Use onOk to control 201 + Location + envelope
            return res.ToActionResult(
                this,
                onOk: id => CreatedAtAction(nameof(Get), new { id }, new SuccessResponse<object>
                {
                    Data = new { id },
                    Message = "Word created.",
                    TraceId = HttpContext.TraceIdentifier
                })
            );
        }

        /// <summary>
        /// Update an existing word.
        /// </summary>
        /// <remarks>
        /// Returns <c>204 No Content</c> on success; <c>409</c> if the target name conflicts.
        /// </remarks>
        /// <param name="id">Word identifier.</param>
        /// <param name="req">Update payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="204">Updated.</response>
        /// <response code="400">Validation failure.</response>
        /// <response code="404">Not found.</response>
        /// <response code="409">Conflict (duplicate word).</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateSensitiveWordRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Word))
                return ValidationProblem("Word is required.");

            var res = await _svc.UpdateAsync(id, req.Word, req.IsActive, ct);
            return res.ToActionResult(this, _ => NoContent());
        }

        /// <summary>
        /// Delete a word.
        /// </summary>
        /// <remarks>
        /// Soft-delete by default. Pass <c>?hard=true</c> to hard-delete.
        /// </remarks>
        /// <param name="id">Word identifier.</param>
        /// <param name="hard">If true, permanently deletes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="204">Deleted.</response>
        /// <response code="404">Not found.</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] int id, [FromQuery] bool hard = false, CancellationToken ct = default)
            => (await _svc.DeleteAsync(id, hard, ct)).ToActionResult(this, _ => NoContent());
    }
}
