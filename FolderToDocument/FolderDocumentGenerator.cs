using System.Text;
using System.Text.RegularExpressions;

namespace FolderToDocument;

/// <summary>
/// 文件夹文档生成器 - AI 交互增强版
/// 提供包含依赖解析、行号标注、AI 指令嵌入等深度优化功能
/// </summary>
public class FolderDocumentGenerator
{
    // 排除的扩展名：使用 HashSet 优化 $O(1)$ 查找性能，过滤二进制和资源文件
    private readonly HashSet<string> _excludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".cache", ".user", ".suo",
        ".jpg", ".png", ".gif", ".ico", ".pdf", ".zip", ".rar", ".7z", ".map", ".bmp"
    };

    // 排除的文件夹：使用 HashSet 优化 $O(1)$ 查找性能，过滤构建输出和环境文件夹
    private readonly HashSet<string> _excludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages",
        "Debug", "Release", ".idea", "dist", "build", "__pycache__", "Properties"
    };

    // 脱敏正则：保护隐私信息
    private static readonly List<(Regex pattern, string replacement)> _sensitivePatterns = new()
    {
        (new Regex(@"ConnectionString\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "ConnectionString=\"***\""),
        (new Regex(@"\b(?:AppKey|Secret|Password|Token|Pwd|ApiKey|RegCode)\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Property=\"***\""),
        (new Regex(@"\b[0-9a-f]{32,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "***HEX_SECRET***"),
        (new Regex(@"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "user@example.com")
    };

    private List<Regex> _includeRegexes = new();

    /// <summary>
    /// 生成完整项目文档，并针对 AI 交互进行深度优化
    /// </summary>
    /// <param name="rootPath">项目根路径</param>
    /// <param name="outputPath">输出路径（可选）</param>
    /// <param name="includedPatterns">包含模式（可选）</param>
    /// <param name="taskMode">咨询模式 (debug/optimize)</param>
    /// <param name="customRequirements">自定义附加要求清单（可选）</param> 
    /// <returns>生成的文档路径</returns>
    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null,
        List<string> includedPatterns = null, string taskMode = "optimize",
        List<string> customRequirements = null)
    {
        if (rootPath == null) throw new ArgumentNullException(nameof(rootPath));
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"目录不存在: {rootPath}");
        try
        {
            _includeRegexes = PrepareIncludeRegexes(includedPatterns);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Regex initialization failed", ex);
        }

        string projectName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(outputPath))
        {
            DirectoryInfo currentInfo = new DirectoryInfo(rootPath);
            DirectoryInfo parentInfo = currentInfo.Parent;
            if (parentInfo != null && parentInfo.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                parentInfo = parentInfo.Parent;
            string baseDir = parentInfo?.FullName ?? rootPath;
            outputPath = Path.Combine(baseDir, "Md", projectName, $"{projectName}.md");
        }

        Console.WriteLine($"[1/5] 正在解析项目元数据与依赖...");
        string projectMetadata = await GetProjectMetadataAsync(rootPath);

        var fullDoc = new StringBuilder();
        var contentBody = new StringBuilder();

        // 1. 根据模式生成不同的 System Prompt
        fullDoc.AppendLine("<TaskDefinition>");
        fullDoc.AppendLine("## ROLE: Senior Software Architect");
        if (taskMode == "debug")
        {
            fullDoc.AppendLine("## MODE: CRITICAL_DEBUG_REPAIR");
            fullDoc.AppendLine("- TASK_1: Analyze the provided error logs and pinpoint the exact source line.");
            fullDoc.AppendLine("- TASK_2: Trace cross-file dependencies and explain the root cause.");
        }
        else
        {
            fullDoc.AppendLine("## MODE: CODE_OPTIMIZATION_REVIEW");
            fullDoc.AppendLine("- TASK_1: Audit concurrency safety, memory leaks, and logical flaws.");
            fullDoc.AppendLine("- TASK_2: Refactor for performance while maintaining backward compatibility.");
        }

        fullDoc.AppendLine("</TaskDefinition>\n");
        // 插入自定义需求
        if (customRequirements != null && customRequirements.Count > 0)
        {
            fullDoc.AppendLine("> 3. **专项要求**：");
            foreach (var req in customRequirements)
            {
                fullDoc.AppendLine($">    - {req}");
            }
        }

        // 统一的输出规范 (引入强制格式模板以确保 AI 严格执行 5 行上下文)
        int specIndex = (customRequirements?.Count > 0) ? 4 : 3;
        fullDoc.AppendLine("<OutputStrictConstraint>");
        fullDoc.AppendLine($"- RULE_1: You MUST output using the following Markdown format for EVERY change.");
        fullDoc.AppendLine("- RULE_2: Context lines (at least 5 lines before/after) are MANDATORY.");
        fullDoc.AppendLine("- RULE_3: Original line numbers (e.g., 001 |) MUST be preserved in both blocks.");
        fullDoc.AppendLine("### [FULL_FILE_PATH]");
        fullDoc.AppendLine("**【修改前】(Line XXX-XXX)**:");
        fullDoc.AppendLine("```[lang]");
        fullDoc.AppendLine("... source code with line numbers ...");
        fullDoc.AppendLine("```");
        fullDoc.AppendLine("**【修改后】(Line XXX-XXX)**:");
        fullDoc.AppendLine("```[lang]");
        fullDoc.AppendLine(
            $"... modified code with line numbers and // <--- {(taskMode == "debug" ? "FIXED" : "Optimization")} annotation ...");
        fullDoc.AppendLine("```");
        fullDoc.AppendLine("</OutputStrictConstraint>");


        fullDoc.AppendLine("\n---\n");

        // 2. 项目基本信息与依赖
        fullDoc.AppendLine($"# {projectName} 项目文档");
        fullDoc.AppendLine($"> 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        fullDoc.AppendLine($"- **项目根目录**: `{rootPath}`");
        fullDoc.Append(projectMetadata);
        fullDoc.AppendLine();

        // 3. 构建目录树
        Console.WriteLine("[2/5] 正在构建视觉化目录树...");
        fullDoc.AppendLine("## 1. 项目目录结构");
        fullDoc.AppendLine("```text");
        fullDoc.AppendLine($"{projectName}/");
        BuildTreeRecursive(rootPath, rootPath, "", fullDoc);
        fullDoc.AppendLine("```\n---\n");

        // 4. 提取代码内容（带行号）与 ToC
        Console.WriteLine("[3/5] 正在提取源码并标注行号...");
        var stats = await ProcessDirectoryWithStatsAsync(rootPath, rootPath, contentBody);

        // 5. 组装索引
        fullDoc.Append(contentBody.ToString());
        fullDoc.AppendLine("\n<ImportantReminder>");
        fullDoc.AppendLine("Source code loading complete. Review the logic above and apply ROLE definitions.");
        fullDoc.AppendLine("Remember: Use RULE_1 to RULE_3 for any code output.");
        fullDoc.AppendLine("</ImportantReminder>"); 

        // 6. 统计信息与 Token 估算
        fullDoc.AppendLine("\n---");
        fullDoc.AppendLine("## 3. 项目规模与统计");
        fullDoc.AppendLine($"- **文件总数**: {stats.FileCount}");
        fullDoc.AppendLine($"- **代码总行数**: {stats.LineCount}");
        fullDoc.AppendLine($"- **Token 估算 (基于字符数)**: ~{(fullDoc.Length / 4):N0} Tokens");
        fullDoc.AppendLine("- **安全状态**: 已自动执行正则脱敏");

        Console.WriteLine("[4/5] 正在执行文件写入...");
        string outputDir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
        await File.WriteAllTextAsync(outputPath, fullDoc.ToString(), Encoding.UTF8);

        Console.WriteLine($"[5/5] 文档生成成功！位置: {outputPath}");
        return outputPath;
    }

    /// <summary>
    /// 解析项目中的所有 .csproj 文件，提取框架版本和 NuGet 依赖
    /// </summary>
    private async Task<string> GetProjectMetadataAsync(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath)) return string.Empty;
        var csprojFiles = Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
        if (csprojFiles.Length == 0) return "- **依赖信息**: 未发现 .csproj 文件\n";

        var sb = new StringBuilder();
        sb.AppendLine("- **项目技术栈与依赖库**:");
        foreach (var file in csprojFiles)
        {
            if (!File.Exists(file)) continue;
            string content = await File.ReadAllTextAsync(file);
            var frameworkMatch =
                Regex.Match(content, @"<TargetFramework>(.*?)</TargetFramework>", RegexOptions.Compiled);
            string framework = frameworkMatch.Groups[1].Value;
            sb.AppendLine($"  - **{Path.GetFileName(file)}** (Framework: `{framework}`)");

            var packages = Regex.Matches(content, @"<PackageReference Include=""(.*?)"" Version=""(.*?)"" />",
                RegexOptions.Compiled);
            foreach (Match p in packages)
            {
                sb.AppendLine($"    - `{p.Groups[1].Value}` (v{p.Groups[2].Value})");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 预编译正则模式，支持 Glob 风格路径筛选
    /// </summary>
    private List<Regex> PrepareIncludeRegexes(List<string> patterns)
    {
        var result = new List<Regex>();
        if (patterns == null || !patterns.Any()) return result;

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;
            string regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*/", "(.+/)?")
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".") + "$";
            result.Add(new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        return result;
    }

    /// <summary>
    /// 判断文件或路径是否在用户指定的包含范围内
    /// </summary>
    private bool IsPathIncluded(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return false;
        var regexes = this._includeRegexes;
        if (regexes == null || regexes.Count == 0) return true;

        string normalized = relativePath.Replace("\\", "/");
        return regexes.Any(re => re.IsMatch(normalized) || re.IsMatch(Path.GetFileName(normalized)));
    }

    /// <summary>
    /// 递归构建视觉目录树，并标记 Program.cs 或入口点
    /// </summary>
    private void BuildTreeRecursive(string currentPath, string rootPath, string indent, StringBuilder sb)
    {
        var dirs = Directory.GetDirectories(currentPath)
            .Where(d => !_excludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        var files = Directory.GetFiles(currentPath)
            .Where(f => !_excludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f).ToList();

        var allItems = dirs.Cast<string>().Concat(files.Cast<string>())
            .Where(path => IsPathIncluded(GetRelativePath(path, rootPath)))
            .ToList();

        for (int i = 0; i < allItems.Count; i++)
        {
            try
            {
                bool isLast = (i == allItems.Count - 1);
                var item = allItems[i];
                string name = Path.GetFileName(item);
                bool isDir = Directory.Exists(item);

                // 标注入口点文件，帮助 AI 识别主逻辑起点
                string marker = !isDir && (name.Equals("Program.cs") || name.Equals("App.xaml.cs") ||
                                           name.Equals("Startup.cs") || name.Equals("main.py"))
                    ? " [Entry Point]"
                    : "";

                sb.Append(indent).Append(isLast ? "└── " : "├── ").Append(name).Append(isDir ? "/" : "").Append(marker)
                    .AppendLine();

                if (isDir)
                {
                    BuildTreeRecursive(item, rootPath, indent + (isLast ? "    " : "│   "), sb);
                }
            }
            catch (UnauthorizedAccessException)
            {
                sb.Append(indent).Append("└── [Access Denied]").AppendLine();
            }
        }
    }

    public record ProjectStats(int FileCount, long LineCount);

    /// <summary>
    /// 处理目录下的代码文件：提取源码、标注行号、应用脱敏、生成锚点跳转
    /// </summary>
    private async Task<ProjectStats> ProcessDirectoryWithStatsAsync(string currentPath, string rootPath,
        StringBuilder contentSb)
    {
        int fCount = 0;
        long lCount = 0;

        var files = Directory.GetFiles(currentPath)
            .Where(f => !_excludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .Where(f => IsPathIncluded(GetRelativePath(f, rootPath)))
            .OrderBy(f => f).ToList();

        foreach (var file in files)
        {
            if (file == null) continue;
            string relPath = GetRelativePath(file, rootPath);

            contentSb.AppendLine($"[FILE: {relPath}]");

            string rawContent = await File.ReadAllTextAsync(file);
            if (IsConfigFile(file)) rawContent = SanitizeSensitiveInfo(rawContent);

            // 标注行号：使得 AI 回复时可以精准定位
            var lines = rawContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string fence = rawContent.Contains("```") ? "~~~~" : "```";
            contentSb.AppendLine($"{fence}{GetFileExtension(file)}");
            for (int i = 0; i < lines.Length; i++)
            {
                contentSb.Append(i + 1).Append('|').AppendLine(lines[i].TrimEnd());
            }

            contentSb.AppendLine(fence);
            fCount++;
            lCount += lines.Length;


            Console.WriteLine($"[写入] {relPath}");
        }

        var directories = Directory.GetDirectories(currentPath)
            .Where(d => !_excludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        foreach (var directory in directories)
        {
            var subStats = await ProcessDirectoryWithStatsAsync(directory, rootPath, contentSb);
            fCount += subStats.FileCount;
            lCount += subStats.LineCount;
        }

        return new ProjectStats(fCount, lCount);
    }

    private string GetRelativePath(string fullPath, string rootPath) => Path.GetRelativePath(rootPath, fullPath);

    /// <summary>
    /// 判断文件是否为配置文件，决定是否执行敏感信息脱敏
    /// </summary>
    private bool IsConfigFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        string fn = Path.GetFileName(filePath).ToLower();
        return ext == ".json" || ext == ".config" || ext == ".xml" || fn.Contains("setting") || fn.Contains("constant");
    }

    /// <summary>
    /// 执行正则替换脱敏逻辑
    /// </summary>
    private string SanitizeSensitiveInfo(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        foreach (var (pattern, replacement) in _sensitivePatterns) content = pattern.Replace(content, replacement);
        return content;
    }

    /// <summary>
    /// 返回 Markdown 适用的代码语言标识符
    /// </summary>
    private string GetFileExtension(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".cs" => "csharp",
            ".json" => "json",
            ".xml" => "xml",
            ".csproj" => "xml",
            ".md" => "markdown",
            _ => "text"
        };
    }
}