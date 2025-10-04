using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SensitiveWords.Application.Attributes;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace SensitiveWords.API
{
    public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider;

        public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        {
            _provider = provider;
        }

        public void Configure(SwaggerGenOptions opt)
        {
            // Include XML docs (controller-level comments require includeControllerXmlComments: true)
            var asm = Assembly.GetExecutingAssembly();
            var xmlFiles = new[]
            {
        Path.Combine(AppContext.BaseDirectory, $"{asm.GetName().Name}.xml"),

        // Add other assemblies that contain controllers/DTOs if needed:
         Path.Combine(AppContext.BaseDirectory, "SensitiveWords.Application.xml"),
         Path.Combine(AppContext.BaseDirectory, "SensitiveWords.Domain.xml"),
         Path.Combine(AppContext.BaseDirectory, "SensitiveWords.Infrastructure.xml"),
    };

            foreach (var xml in xmlFiles)
                if (File.Exists(xml))
                    opt.IncludeXmlComments(xml, includeControllerXmlComments: true);

            // Register our filter, pass XML paths so it can read <summary>/<remarks>
            opt.DocumentFilter<ControllerRemarksToTagDescriptionFilter>();

            opt.CustomSchemaIds(FriendlySchemaId);

            opt.EnableAnnotations();

            #region Internal + External Doc Descriptions

            var internalDesc = """
**INTERNAL API (Internal only)**  
These endpoints are for back-office or service-to-service use. In production, expose them only behind an API gateway / auth proxy / private network.

**Swagger visibility**  
- Serve Swagger only on internal networks, gateways or require authentication.  
- Filter by audience/version via DocInclusionPredicate (e.g., `internal-v1.0`).  
- For public deployments, use `[ApiExplorerSettings(IgnoreApi = true)]` or exclude via filters.
""";

            var externalDesc = """
**EXTERNAL API — Message “Blooping” Endpoint**

Public-facing endpoint to scan a message and mask sensitive words/phrases.
Intended for first-party clients (web/app/services).  
Returns a `BloopResponseDto` directly (no envelope) for compactness.

**Security**
Protect this route with your API gateway and authentication (JWT/OIDC).  
Apply the `BloopPerHour` rate-limit policy to deter abuse.

**Example**
```http
POST /api/v1.0/messages/bloop
{"message": "Please don't DROP TABLE users;",
  "wholeWord": true
}

Returns:
{
  "original": "Please don't DROP TABLE users;",
  "blooped":  "Please don't **** ***** users;",
  "matches":  2,
  "elapsedMs": 3
}


""";
            #endregion


            // Register {audience × version}
            var audiences = new[] { AudienceAttribute.Internal, AudienceAttribute.External };
            foreach (var desc in _provider.ApiVersionDescriptions)            // "v1.0"
                foreach (var aud in audiences)
                {
                    var docName = $"{aud}-{desc.GroupName}";                      // "internal-v1.0"
                    var isInternal = aud.Equals(AudienceAttribute.Internal, StringComparison.OrdinalIgnoreCase);

                    opt.SwaggerDoc(docName, new OpenApiInfo
                    {
                        Title = $"SensitiveWords ({aud.ToUpperInvariant()})",
                        Version = desc.ApiVersion.ToString(),
                        Description = isInternal
        ? internalDesc
        : $"Audience: {aud}, API Version: {desc.ApiVersion}\n\n{externalDesc}"
                    });
                }

            // Inclusion predicate (accepts v1 or v1.0)
            opt.DocInclusionPredicate((docName, apiDesc) =>
            {
                var parts = docName.Split('-', 2);
                if (parts.Length != 2) return false;

                var docAudience = parts[0];
                var docVersion = parts[1]; // "v1" or "v1.0"

                // Resolve audience from attributes
                var actionAudience = AudienceAttribute.External;
                if (apiDesc.ActionDescriptor is ControllerActionDescriptor cad)
                {
                    var methodAttr = cad.MethodInfo.GetCustomAttribute<AudienceAttribute>(true);
                    var ctrlAttr = cad.ControllerTypeInfo.GetCustomAttribute<AudienceAttribute>(true);
                    actionAudience = methodAttr?.Value ?? ctrlAttr?.Value ?? AudienceAttribute.External;
                }
                else
                {
                    var metaAttr = apiDesc.ActionDescriptor.EndpointMetadata?.OfType<AudienceAttribute>().FirstOrDefault();
                    actionAudience = metaAttr?.Value ?? AudienceAttribute.External;
                }

                // Resolve version
                var ver = apiDesc.GetApiVersion();
                if (ver is null) return false;

                var vMajor = $"v{ver.MajorVersion}";
                var vMajorMinor = $"v{ver}";

                return actionAudience.Equals(docAudience, StringComparison.OrdinalIgnoreCase) &&
                       (docVersion.Equals(vMajor, StringComparison.OrdinalIgnoreCase) ||
                        docVersion.Equals(vMajorMinor, StringComparison.OrdinalIgnoreCase));
            });
        }

        static string FriendlySchemaId(Type t)
        {
            // Arrays
            if (t.IsArray) return $"{FriendlySchemaId(t.GetElementType()!)}[]";

            // Nullable<T> → T?
            if (Nullable.GetUnderlyingType(t) is Type u)
                return $"{FriendlySchemaId(u)}?";

            // Generics → Name«Arg1» or Name«Arg1»«Arg2»
            if (t.IsGenericType)
            {
                var name = t.Name[..t.Name.IndexOf('`')]; // drop `1
                var args = t.GetGenericArguments().Select(FriendlySchemaId);
                return $"{name}«{string.Join("»«", args)}»";
            }

            // Non-generic → just the simple name
            return t.Name;
        }
    }
}
