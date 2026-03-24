using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using FolderToDocument.Interfaces;

namespace FolderToDocument.Services;

/// <summary>内容处理器实现</summary>
public partial class ContentProcessor : IContentProcessor
{
    private static readonly List<(Regex pattern, string replacement)> SensitivePatterns =
    [
        (new Regex(@"ConnectionString\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)),
            "ConnectionString=\"***\""),

        (new Regex(@"(?:AppKey|Secret|Password|Token|Pwd|ApiKey|RegCode)\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)),
            "Property=\"***\""),

        (new Regex(@"[""'][0-9a-fA-F]{32,}[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)),
            "\"***HEX_SECRET***\""),

        (new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)),
            "user@example.com")
    ];

    public string SanitizeSensitiveInfo(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        string processed = content;

        foreach (var (pattern, replacement) in SensitivePatterns)
        {
            try
            {
                processed = pattern.Replace(processed, replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }
        }

        return processed;
    }

    public string StripComments(string content, string extension)
    {
        if (extension is not (".cs" or ".js" or ".ts" or ".json"))
            return content;

        return CommentStripRegex().Replace(content, m =>
        {
            if (m.Groups[1].Success) return m.Groups[1].Value;
            return string.Empty;
        });
    }

    [GeneratedRegex(@"(@""(?:[^""]|"""")*""|""(?:\\.|[^\\""])*""|'(?:\\.|[^\\'])*')|//.*|/\*[\s\S]*?\*/",
        RegexOptions.Multiline)]
    private static partial Regex CommentStripRegex();

    public string ExtractJsonSchema(string json, int maxArrayItems = 1)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        try
        {
            using var doc = JsonDocument.Parse(json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            var sb = new StringBuilder();
            WriteJsonElement(doc.RootElement, sb, 0, maxArrayItems);
            return sb.ToString();
        }
        catch
        {
            const int maxFallback = 600;
            return json.Length > maxFallback
                ? json[..maxFallback] + "\n// ... [截断，仅供结构参考]"
                : json;
        }
    }

    private static void WriteJsonElement(
        JsonElement element,
        StringBuilder sb,
        int depth,
        int maxArrayItems)
    {
        string indent = new string(' ', depth * 2);
        string childIndent = new string(' ', (depth + 1) * 2);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.AppendLine("{");
                var props = element.EnumerateObject().ToList();
                for (int i = 0; i < props.Count; i++)
                {
                    var prop = props[i];
                    sb.Append($"{childIndent}\"{prop.Name}\": ");
                    WriteJsonElement(prop.Value, sb, depth + 1, maxArrayItems);
                    if (i < props.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.Append($"{indent}}}");
                break;

            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                if (items.Count == 0)
                {
                    sb.Append("[]");
                    break;
                }

                sb.AppendLine("[");

                int showCount = Math.Min(maxArrayItems, items.Count);
                for (int i = 0; i < showCount; i++)
                {
                    sb.Append(childIndent);
                    WriteJsonElement(items[i], sb, depth + 1, maxArrayItems);
                    if (i < showCount - 1 || items.Count > showCount) sb.Append(",");
                    sb.AppendLine();
                }

                if (items.Count > showCount)
                {
                    sb.AppendLine($"{childIndent}// ... 共 {items.Count} 项，此处仅展示结构schema");
                }

                sb.Append($"{indent}]");
                break;

            case JsonValueKind.String:
                string strVal = element.GetString() ?? "";
                if (strVal.Length > 40)
                    strVal = strVal[..37] + "...";
                sb.Append($"\"{JsonEncodedText.Encode(strVal)}\"");
                break;

            case JsonValueKind.Number:
                sb.Append(element.GetRawText());
                break;

            case JsonValueKind.True:
                sb.Append("true");
                break;

            case JsonValueKind.False:
                sb.Append("false");
                break;

            case JsonValueKind.Null:
                sb.Append("null");
                break;

            default:
                sb.Append(element.GetRawText());
                break;
        }
    }

    public string GetFileExtensionForMarkdown(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" or ".razor" => "csharp",
            ".json" or ".settings" or ".dev" => "json",
            ".xml" or ".csproj" or ".targets" or ".props" or ".config" => "xml",
            ".md" => "markdown",
            ".js" or ".ts" => "javascript",
            ".sql" => "sql",
            ".yaml" or ".yml" => "yaml",
            _ => "text"
        };
    }
}