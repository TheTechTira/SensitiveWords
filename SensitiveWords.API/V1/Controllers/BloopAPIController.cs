using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Domain.Dtos;

namespace SensitiveWords.API.Controllers
{
    // External endpoint to star out sensitive words/phrases in a message
    [ApiController]
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/messages")]
    [Audience(AudienceAttribute.External)]
    [Produces("application/json")]
    [Tags("Bloop")]
    public class BloopAPIController : ControllerBase
    {


        /// <summary>Stars out sensitive words/phrases in a message.</summary>
        [HttpPost("bloop")]
        public IActionResult Bloop()
        {
            throw new NotImplementedException();
        }
       
    }
}
