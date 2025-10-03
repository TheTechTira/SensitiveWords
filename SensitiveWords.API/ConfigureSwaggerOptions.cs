using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SensitiveWords.Application.Abstractions.Services;
using SensitiveWords.Application.Attributes;
using SensitiveWords.Application.Common.Responses;
using SensitiveWords.Domain.Dtos;
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
            // include XML for API + referenced assemblies that have models
            var assemblies = new[]
            {
        Assembly.GetExecutingAssembly(),        // API (controllers)
        //typeof(SensitiveWordDto).Assembly,      
        //typeof(ErrorResponse).Assembly
    }.Distinct();

            foreach (var asm in assemblies)
            {
                // try AppContext first (debug/publish), then fall back to the assembly’s own folder
                var xml1 = Path.Combine(AppContext.BaseDirectory, $"{asm.GetName().Name}.xml");
                var xml2 = string.IsNullOrEmpty(asm.Location) ? null : Path.ChangeExtension(asm.Location, ".xml");

                if (File.Exists(xml1)) opt.IncludeXmlComments(xml1, includeControllerXmlComments: true);
                else if (xml2 is not null && File.Exists(xml2)) opt.IncludeXmlComments(xml2, includeControllerXmlComments: true);
            }

            opt.CustomSchemaIds(FriendlySchemaId);

            opt.EnableAnnotations();

            // Register {audience × version}
            var audiences = new[] { AudienceAttribute.Internal, AudienceAttribute.External };
            foreach (var desc in _provider.ApiVersionDescriptions)            // "v1.0"
                foreach (var aud in audiences)
                {
                    var docName = $"{aud}-{desc.GroupName}";                      // "internal-v1.0"
                    opt.SwaggerDoc(docName, new OpenApiInfo
                    {
                        Title = $"SensitiveWords ({aud.ToUpperInvariant()})",
                        Version = desc.ApiVersion.ToString(),
                        Description = $"Audience: {aud}, API Version: {desc.ApiVersion}"
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
