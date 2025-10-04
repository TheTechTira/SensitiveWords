using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SensitiveWords.API;
using SensitiveWords.API.V1.Extensions;
using SensitiveWords.API.V1.Filters;
using SensitiveWords.Application.Abstractions.Caching;
using SensitiveWords.Application.Abstractions.Data;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Application.Common.Options;
using SensitiveWords.Application.Common.Responses;
using SensitiveWords.Application.Services;
using SensitiveWords.Domain.ValueObjects;
using SensitiveWords.Infrastructure.Caching;
using SensitiveWords.Infrastructure.Data;
using SensitiveWords.Infrastructure.Repositories;
using SensitiveWords.Infrastructure.Seed;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


// Config
var cs = builder.Configuration.GetConnectionString("Sql")
         ?? throw new ArgumentNullException("SQL Connection String not setup in appsettings.json");

// Controllers + filters
builder.Services.AddControllers(opts =>
{
    opts.Filters.Add<ApiExceptionFilter>(); // global exception handler
    opts.Filters.Add<PaginationHeadersFilter>(); // global pagination results auto adds header option
});
builder.Services.UseStandardModelValidation();

// Add sensitive word policies from config
builder.Services.Configure<SensitiveWordPolicyOptions>(builder.Configuration.GetSection("SensitiveWordPolicy"));

#region DI

// Options pattern
builder.Services.AddOptions();

#region Caching

// In-memory cache for compiled regex
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IWordsCache, WordsCache>();

#endregion

#region DB

// Dapper + SQL Server
builder.Services.AddSingleton<IDapperExecutor, DapperExecutor>();
builder.Services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(cs));

#endregion

#region Repositories

builder.Services.AddScoped<ISensitiveWordRepository, SensitiveWordRepository>();

#endregion

#region Services

// word crud service
builder.Services.AddScoped<ISensitiveWordService, SensitiveWordService>();

// scoped bloop service using the cache
builder.Services.AddScoped<IBloopService, BloopService>();

#endregion

#endregion

#region Swagger

// API versioning
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
    o.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"),
        new QueryStringApiVersionReader("api-version"));
})
.AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";  // produces v1, v1.0, etc.
    o.SubstituteApiVersionInUrl = true;
});


builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

#endregion

#region Rate Limiting

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // If you’re behind a known proxy/load balancer, register its IP(s)
    // Example: AWS ALB, Nginx reverse proxy, etc.
    //o.KnownProxies.Add(IPAddress.Parse("your-proxy-ip"));
    //o.KnownProxies.Add(IPAddress.Parse("123.45.67.89"));
});

// Rate limiter constants
const int BLOOP_LIMIT = 100;
var BLOOP_WINDOW = TimeSpan.FromHours(1);

// Future TODO/NOTE: make this user/session/token specific, this is just a generalized implementation for demostration purposes

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        // Add standard headers
        context.HttpContext.Response.Headers["Retry-After-Seconds"] = ((int)BLOOP_WINDOW.TotalSeconds).ToString();

        // Optionally expose a structured JSON error
        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Type = "https://httpstatuses.com/429",
                Title = "Too Many Requests",
                Status = 429,
                TraceId = context.HttpContext.TraceIdentifier,
                Detail = "Rate limit exceeded. Try again later."
            }, ct);
        }
    };

    options.AddPolicy("BloopPerHour", http =>
    {
        // Prefer forwarded IP if behind proxy (UseForwardedHeaders will set RemoteIpAddress)
        var ip = http.Connection.RemoteIpAddress;

        // Normalize to a stable string (IPv6-mapped IPv4 handled)
        var keyIp = ip is null
            ? "unknown"
            : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();

        var key = $"ip:{keyIp}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = BLOOP_LIMIT,                 // 5 requests
                Window = BLOOP_WINDOW,  // per hour
                QueueLimit = 0,                  // no queue → immediate 429
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

#endregion

var app = builder.Build();

#region Seed Sensitive Words

// Run seeding once on startup (dev/test or first run)
using (var scope = app.Services.CreateScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<ISensitiveWordRepository>();
    var filePath = Path.Combine(AppContext.BaseDirectory, "sql_sensitive_list.txt");
    await WordSeeder.SeedFromFileAsync(filePath, repo);
}

#endregion

// Logging, tracing, forwarded headers, etc.
app.UseForwardedHeaders();

/*
 
Internal vs External Endpoint availability:

"Internal consumption" means the CRUD endpoints are for our own systems/admins, not exposed publicly. We secure and isolate them (network + auth), and we generate a separate internal Swagger.

"External consumption" is the single bloop endpoint that the client calls. It has stricter public controls (rate limiting, CORS, JWT), and a separate external Swagger definition.

We should use a simple reverse-proxy config (NGINX/YARP) that exposes only /api/v1/messages/* to the internet and blocks /api/v1/internal/*. 

 */

app.UseSwagger();
app.UseSwaggerUI(ui =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    var audiences = new[] { AudienceAttribute.Internal, AudienceAttribute.External };

    foreach (var desc in provider.ApiVersionDescriptions)             // desc.GroupName == "v1.0"
        foreach (var aud in audiences)                                // "internal" / "external"
        {
            var group = $"{aud}-{desc.GroupName}";                    // e.g. "internal-v1.0"
            ui.SwaggerEndpoint($"/swagger/{group}/swagger.json",         
                $"SensitiveWords {aud.ToUpperInvariant()} {desc.GroupName}");
        }

    ui.DocumentTitle = "SensitiveWords API";

    ui.DisplayRequestDuration();
});

app.UseHttpsRedirection();

app.UseAuthorization();

// Rate limiting (built-in)
app.UseRateLimiter();

#region Custom Rate Limiting Response

// ---- Counter middleware: adds X-RateLimit-* on success and 429s ----
// Future TODO/NOTE: make this configurable in production per client or per key or per route
app.Use(async (ctx, next) =>
{
    // Only for endpoints using our policy
    var hasPolicy = ctx.GetEndpoint()?.Metadata
        .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "BloopPerHour";

    if (!hasPolicy)
    {
        await next();
        return;
    }

    var cache = ctx.RequestServices.GetRequiredService<IMemoryCache>();

    // Same key as the policy
    var ip = ctx.Connection.RemoteIpAddress;
    var keyIp = ip is null ? "unknown" : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
    var key = $"ip:{keyIp}";

    var state = cache.GetOrCreate(key, entry =>
    {
        var resetAt = DateTimeOffset.UtcNow.Add(BLOOP_WINDOW);
        entry.AbsoluteExpiration = resetAt;
        return new RateLimitCounterState { Count = 0, ResetAtUtc = resetAt };
    })!;

    // Register header writer BEFORE pipeline continues
    ctx.Response.OnStarting(() =>
    {
        // If the limiter rejected earlier, StatusCode will be 429 here
        if (ctx.Response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            ctx.Response.Headers["X-RateLimit-Limit"] = BLOOP_LIMIT.ToString();
            ctx.Response.Headers["X-RateLimit-Remaining"] = "0";
            ctx.Response.Headers["X-RateLimit-Reset"] = state.ResetAtUtc.ToUnixTimeSeconds().ToString();

            var retryAfter = (int)Math.Max(0, (state.ResetAtUtc - DateTimeOffset.UtcNow).TotalSeconds);
            ctx.Response.Headers["Retry-After-Seconds"] = retryAfter.ToString();
        }
        else
        {
            // Successful request: increment count and emit headers
            var used = Interlocked.Increment(ref state.Count);
            var remaining = Math.Max(0, BLOOP_LIMIT - (int)used);

            ctx.Response.Headers["X-RateLimit-Limit"] = BLOOP_LIMIT.ToString();
            ctx.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            ctx.Response.Headers["X-RateLimit-Reset"] = state.ResetAtUtc.ToUnixTimeSeconds().ToString();
        }

        return Task.CompletedTask;
    });

    await next();
});

#endregion

app.MapControllers();

app.Run();