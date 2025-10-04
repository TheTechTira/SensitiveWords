using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Xml.Linq;

namespace SensitiveWords.API
{
    public class ControllerRemarksToTagDescriptionFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Load the executing assembly XML only (matches IncludeXmlComments in Program.cs)
            var baseDir = AppContext.BaseDirectory;
            var apiXml = Directory.EnumerateFiles(baseDir, "*.xml")
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x)
                    .Equals(AppDomain.CurrentDomain.FriendlyName, StringComparison.OrdinalIgnoreCase))
                ?? Directory.EnumerateFiles(baseDir, "*.xml").FirstOrDefault(); // fallback

            var docs = LoadControllerDocsAsMarkdown(apiXml is string p && File.Exists(p) ? new[] { p } : Array.Empty<string>());

            // Map tag -> controller full name (prefer [Tags], fallback to controller name)
            var tagToController = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var d in context.ApiDescriptions)
            {
                if (d.ActionDescriptor is not ControllerActionDescriptor cad) continue;
                var fullName = cad.ControllerTypeInfo?.FullName;
                if (string.IsNullOrWhiteSpace(fullName)) continue;

                var tag = cad.EndpointMetadata?.OfType<TagsAttribute>().FirstOrDefault()?.Tags?.FirstOrDefault()
                          ?? cad.ControllerName;
                if (string.IsNullOrWhiteSpace(tag)) continue;

                if (!tagToController.ContainsKey(tag))
                    tagToController[tag] = fullName; // first wins
            }

            swaggerDoc.Tags ??= new List<OpenApiTag>();

            // Ensure all used tags exist
            var used = context.ApiDescriptions
                .Select(d => d.ActionDescriptor is ControllerActionDescriptor cad
                    ? cad.EndpointMetadata?.OfType<TagsAttribute>().FirstOrDefault()?.Tags?.FirstOrDefault() ?? cad.ControllerName
                    : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.Ordinal);

            var existing = new HashSet<string>(swaggerDoc.Tags.Select(t => t.Name), StringComparer.Ordinal);
            foreach (var name in used)
                if (!existing.Contains(name!))
                    swaggerDoc.Tags.Add(new OpenApiTag { Name = name! });

            // Inject description (Markdown). Header still shows one line; full Markdown appears when expanded.
            foreach (var tag in swaggerDoc.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Name)) continue;
                if (!tagToController.TryGetValue(tag.Name, out var fullName)) continue;
                if (!docs.TryGetValue(fullName, out var md)) continue;

                if (string.IsNullOrWhiteSpace(tag.Description))
                    tag.Description = md; // Markdown shows multi-line when accordion is expanded
            }
        }

        // ---------- XML -> Markdown ----------

        private static Dictionary<string, string> LoadControllerDocsAsMarkdown(IEnumerable<string> xmlPaths)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var xmlPath in xmlPaths)
            {
                XDocument xdoc;
                try { xdoc = XDocument.Load(xmlPath); } catch { continue; }

                var members = xdoc.Root?.Element("members")?.Elements("member") ?? Enumerable.Empty<XElement>();
                foreach (var m in members)
                {
                    var name = m.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("T:", StringComparison.Ordinal)) continue;

                    var fullName = name.Substring(2);
                    var summary = XmlToMarkdown(m.Element("summary"));
                    var remarks = XmlToMarkdown(m.Element("remarks"));
                    var combined = Combine(summary, remarks);

                    if (!string.IsNullOrWhiteSpace(combined))
                        map[fullName] = combined;
                }
            }

            return map;
        }

        private static string XmlToMarkdown(XElement? node)
        {
            if (node is null) return string.Empty;
            var sb = new System.Text.StringBuilder();

            void Walk(XNode n)
            {
                switch (n)
                {
                    case XElement e:
                        switch (e.Name.LocalName.ToLowerInvariant())
                        {
                            case "para":
                                foreach (var child in e.Nodes()) Walk(child);
                                sb.AppendLine().AppendLine();
                                break;
                            case "list":
                                foreach (var item in e.Elements("item"))
                                {
                                    var text = Normalize(item.Element("description")?.Value ?? item.Value);
                                    if (!string.IsNullOrWhiteSpace(text))
                                        sb.Append("- ").AppendLine(text);
                                }
                                sb.AppendLine();
                                break;
                            case "c":
                                sb.Append('`').Append(e.Value).Append('`');
                                break;
                            case "b":
                                sb.Append("**").Append(Normalize(e.Value)).Append("**");
                                break;
                            case "see":
                                sb.Append('`').Append(e.Attribute("cref")?.Value ?? e.Value).Append('`');
                                break;
                            default:
                                foreach (var child in e.Nodes()) Walk(child);
                                break;
                        }
                        break;
                    case XText t:
                        sb.Append(t.Value);
                        break;
                }
            }

            foreach (var child in node.Nodes()) Walk(child);

            var md = sb.ToString().Replace("\r", "");
            while (md.Contains("\n\n\n")) md = md.Replace("\n\n\n", "\n\n");
            return md.Trim();
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Replace("\r", "");
            var lines = s.Split('\n').Select(l =>
                System.Text.RegularExpressions.Regex.Replace(l, @"\s+", " ").Trim());
            return string.Join(" ", lines);
        }

        private static string Combine(string summary, string remarks)
        {
            if (string.IsNullOrWhiteSpace(summary)) return remarks ?? string.Empty;
            if (string.IsNullOrWhiteSpace(remarks)) return summary ?? string.Empty;
            return $"{summary}\n\n{remarks}";
        }
    }
}
