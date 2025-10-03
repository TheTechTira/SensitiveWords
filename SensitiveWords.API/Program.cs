using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SensitiveWords.API;
using SensitiveWords.API.Extensions;
using SensitiveWords.API.Filters;
using SensitiveWords.Application.Abstractions.Caching;
using SensitiveWords.Application.Abstractions.Data;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Application.Common.Options;
using SensitiveWords.Application.Services;
using SensitiveWords.Infrastructure.Caching;
using SensitiveWords.Infrastructure.Data;
using SensitiveWords.Infrastructure.Repositories;
using SensitiveWords.Infrastructure.Seed;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

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

// lifetimes
builder.Services.AddOptions();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IDapperExecutor, DapperExecutor>();
builder.Services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(cs));

builder.Services.AddScoped<ISensitiveWordRepository, SensitiveWordRepository>();

// singleton cache wrapper
builder.Services.AddSingleton<IWordsCache, WordsCache>();

// scoped service using the cache
builder.Services.AddScoped<ISensitiveWordService, SensitiveWordService>();

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
 
Plain-English answer you can give

“Internal consumption” means the CRUD endpoints are for our own systems/admins, not exposed publicly. We secure and isolate them (network + auth), and we generate a separate internal Swagger.

“External consumption” is the single bloop endpoint that the client calls. It has stricter public controls (rate limiting, CORS, JWT), and a separate external Swagger definition.

If you want, I can show a simple reverse-proxy config (NGINX/YARP) that exposes only /api/v1/messages/* to the internet and blocks /api/v1/internal/*. 

 */
app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((doc, req) =>
    {
        // Handy debug: prints doc title and how many paths made it in
        Console.WriteLine($"[SWAGGER BUILD] {doc.Info?.Title} -> paths={doc.Paths?.Count ?? 0}");
    });
});
app.UseSwaggerUI(ui =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    var audiences = new[] { AudienceAttribute.Internal, AudienceAttribute.External };

    foreach (var desc in provider.ApiVersionDescriptions)             // desc.GroupName == "v1.0"
        foreach (var aud in audiences)                                    // "internal" / "external"
        {
            var group = $"{aud}-{desc.GroupName}";                         // e.g. "internal-v1.0"
            ui.SwaggerEndpoint($"/swagger/{group}/swagger.json",          // ABSOLUTE path; ".json" spelled right
                $"SensitiveWords {aud.ToUpperInvariant()} {desc.GroupName}");
        }

    ui.DocumentTitle = "SensitiveWords API";
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();