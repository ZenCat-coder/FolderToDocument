using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FolderToDocument;

/// <summary>
/// 文件夹文档生成器核心类。
/// </summary>
public partial class FolderDocumentGenerator
{
    private HashSet<string> ExcludedExtensions { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".cache", ".user", ".suo",
        ".jpg", ".png", ".gif", ".ico", ".pdf", ".zip", ".rar", ".7z", ".map", ".bmp"
    };

    private HashSet<string> ExcludedFolders { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages",
        "Debug", "Release", ".idea", "dist", "build", "__pycache__", "Properties"
    };
    
    private static readonly EnumerationOptions DefaultEnumOptions = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = false
    };

    // 脱敏正则：保护隐私信息
    private static readonly List<(Regex pattern, string replacement)> SensitivePatterns =
    [
        (new Regex(@"ConnectionString\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "ConnectionString=\"***\""),

        (new Regex(@"\b(?:AppKey|Secret|Password|Token|Pwd|ApiKey|RegCode)\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Property=\"***\""),

        (new Regex(@"\b[0-9a-f]{32,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "***HEX_SECRET***"),
        (new Regex(@"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "user@example.com")
    ];

    /// <summary>
    /// 执行文档生成的主逻辑
    /// </summary>
    /// <param name="rootPath">待分析的项目根路径</param>
    /// <param name="outputPath">输出 Markdown 的路径（可选）</param>
    /// <param name="includedPatterns">包含文件的通配符规则（可选）</param>
    /// <param name="taskMode">AI 任务模式（debug/optimize）</param>
    /// <param name="customRequirements">自定义需求描述列表</param>
    /// <returns>生成的文档物理路径</returns>
    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null,
        List<string> includedPatterns = null, string taskMode = "optimize",
        List<string> customRequirements = null)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Source directory not found: {rootPath}");

        var currentRegexes = PrepareIncludeRegexes(includedPatterns);

        string projectName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar)) ?? "Project";

        if (string.IsNullOrEmpty(outputPath))
        {
            var rootInfo = new DirectoryInfo(rootPath);
            string baseDir = rootInfo.Parent?.FullName ?? rootPath;
            outputPath = Path.Combine(baseDir, "Md", projectName, $"{projectName}.md");
        }

        string outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        Console.WriteLine($"[1/5] 正在解析项目元数据...");
        string projectMetadata = await GetProjectMetadataAsync(rootPath);

        await using var sw = new StreamWriter(outputPath, false, Encoding.UTF8);

        await sw.WriteLineAsync("<TaskDefinition>");


        if (taskMode == "explain")
        {
            await sw.WriteLineAsync("## ROLE: Senior Technical Educator");
            await sw.WriteLineAsync("## EXPERTISE: C# Programming, Logic Explanation, Software Engineering Fundamentals");
        }
        else
        {
            await sw.WriteLineAsync("## ROLE: Senior Software Architect");
            await sw.WriteLineAsync("## EXPERTISE: .NET 8, High-Performance Systems, Secure Coding, Clean Architecture");
        }

        await sw.WriteLineAsync("## THOUGHT_PROCESS: Mandatory Chain-of-Thought");
        await sw.WriteLineAsync("- STEP_1: Identify all potential side effects of the proposed change on existing logic.");
        await sw.WriteLineAsync("- STEP_2: Verify if any method signatures are changed (avoid breaking API compatibility).");
        await sw.WriteLineAsync("- STEP_3: Explicitly check for null-reference risks and proper exception handling in new code blocks.");
        await sw.WriteLineAsync("- STEP_4: Confirm that the solution strictly follows .NET 8 best practices.");


        if (taskMode == "debug")
        {
            await sw.WriteLineAsync("## MODE: CRITICAL_DEBUG_REPAIR");
            await sw.WriteLineAsync("- TASK_1: Analyze code and pinpoint the root cause of potential runtime exceptions.");
            await sw.WriteLineAsync("- TASK_2: Provide a thread-safe, memory-efficient fix.");
            await sw.WriteLineAsync("- TASK_3: Explain why the previous logic failed.");
        }
        else if (taskMode == "explain")
        {
            await sw.WriteLineAsync("## MODE: BEGINNER_CODE_WALKTHROUGH");
            await sw.WriteLineAsync("- TASK_1: Explain the high-level workflow of the code in simple terms.");
            await sw.WriteLineAsync("- TASK_2: Break down complex methods and explain the purpose of key variables.");
            await sw.WriteLineAsync("- TASK_3: Highlight common C# patterns used (e.g., async/await, Linq).");
        }
        else
        {
            await sw.WriteLineAsync("## MODE: CODE_OPTIMIZATION_REVIEW");
            await sw.WriteLineAsync("- TASK_1: Audit code for concurrency safety and memory leaks.");
            await sw.WriteLineAsync("- TASK_2: Refactor for performance using Span/ArrayPool.");
            await sw.WriteLineAsync("- TASK_3: Ensure SOLID principles.");
        }

        await sw.WriteLineAsync("- TASK_4: Ensure NO additional external dependencies are introduced.");
        await sw.WriteLineAsync("</TaskDefinition>\n");

        if (customRequirements is { Count: > 0 })
        {
            await sw.WriteLineAsync("> 3. **专项要求**：");
            foreach (var req in customRequirements) await sw.WriteLineAsync($">    - {req}");
        }

        await sw.WriteLineAsync("<OutputStrictConstraint>");
        await sw.WriteLineAsync("- RULE_1: You MUST output using the following Markdown format for EVERY change.");
        await sw.WriteLineAsync("- RULE_2: You MUST provide the ENTIRE method or logic block. DO NOT use snippets (e.g., `...`) or partial updates.");
        await sw.WriteLineAsync("- RULE_3: The [Modified] code block MUST NOT contain line numbers.");
        await sw.WriteLineAsync("- RULE_4: You MUST keep the original code commented out (e.g., `// [Original] code...` or `/* */`) immediately before the new code. DO NOT DELETE the original logic.");
        await sw.WriteLineAsync("- RULE_5: Every modification MUST include a Chinese comment (// <--- 原因) explaining 'WHY' the change was made.");
        await sw.WriteLineAsync("- RULE_6: If a method has NO changes, DO NOT output it. Only output modified methods/logic blocks.");
        await sw.WriteLineAsync("- RULE_7: If a change affects other methods (chain reaction), include ALL affected methods in the output.");
        await sw.WriteLineAsync("- RULE_8: CATEGORIZED OUTPUT: Group findings into SECURITY, PERFORMANCE, LOGIC, ARCHITECTURE.");
        await sw.WriteLineAsync("- RULE_9: You MUST answer in Chinese.");
        // <--- 原因: 新增 RULE_10，强制要求生成的代码必须包含 XML 文档注释 (/// <summary>)，以便于代码维护和理解。
        await sw.WriteLineAsync("- RULE_10: You MUST add XML documentation comments (/// <summary>) for any NEW or MODIFIED methods.");
        await sw.WriteLineAsync("</OutputStrictConstraint>\n\n---\n");

        await sw.WriteLineAsync($"# {projectName} 项目文档");
        await sw.WriteLineAsync($"> 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await sw.WriteLineAsync($"- **项目根目录**: `{rootPath}`");
        await sw.WriteAsync(projectMetadata);
        await sw.WriteLineAsync();

        Console.WriteLine("[2/5] 正在构建目录树...");
        await sw.WriteLineAsync("## 1. 项目目录结构\n```text");
        await sw.WriteLineAsync($"{projectName}/");
        await BuildTreeRecursiveAsync(rootPath, rootPath, "", sw, currentRegexes);
        await sw.WriteLineAsync("```\n---\n");

        Console.WriteLine("[3/5] 正在处理源码并标注行号...");

        var stats = await ProcessDirectoryWithStatsAsync(rootPath, rootPath, sw, currentRegexes, taskMode);

        await sw.WriteLineAsync("\n<ImportantReminder>");
        await sw.WriteLineAsync("System Context Loaded. Current project uses .NET 8 SDK.");
        await sw.WriteLineAsync("Immediate Action: Execute audit and categorize findings per RULE_9.");
        await sw.WriteLineAsync("</ImportantReminder>\n\n---");

        await sw.WriteLineAsync("## 3. 项目规模 with 统计");
        await sw.WriteLineAsync($"- **文件总数**: {stats.FileCount}");
        await sw.WriteLineAsync($"- **代码总行数**: {stats.LineCount}");
        await sw.WriteLineAsync($"- **安全状态**: 已自动执行正则脱敏");

        Console.WriteLine($"[5/5] 文档生成成功！");
        return outputPath;
    }

    /// <summary>
    /// 解析项目元数据，提取 .csproj 中的框架和依赖信息。
    /// </summary>
    private async Task<string> GetProjectMetadataAsync(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath)) return string.Empty;

        var enumerationOptions = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 10 };
        var csprojFiles = Directory.EnumerateFiles(rootPath, "*.csproj", enumerationOptions)
            .Where(f => !ExcludedFolders.Any(ef =>
                f.Contains($"{Path.DirectorySeparatorChar}{ef}{Path.DirectorySeparatorChar}")));

        var sb = new StringBuilder();
        bool found = false;

        foreach (var file in csprojFiles)
        {
            if (!File.Exists(file)) continue;
            if (!found)
            {
                sb.AppendLine("- **项目技术栈与依赖库**:");
                found = true;
            }

            try
            {
                // [Modified] 使用异步流式加载，比 ReadAllText 更节省内存
                await using var stream = File.OpenRead(file);
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

                var projectElement = doc.Element("Project");
                // 查找 TargetFramework 或 TargetFrameworks
                var framework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                                ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value
                                ?? "Unknown Framework";

                sb.AppendLine($"  - **{Path.GetFileName(file)}** (Framework: `{framework}`)");

                var packages = doc.Descendants("PackageReference");
                foreach (var p in packages)
                {
                    var name = p.Attribute("Include")?.Value;
                    var ver = p.Attribute("Version")?.Value ?? "Latest";
                    if (!string.IsNullOrEmpty(name))
                    {
                        sb.AppendLine($"    - `{name}` (v{ver})");
                    }
                }
            }
            catch (Exception ex)
            {
                // 容错处理：如果 XML 格式错误，记录警告但不中断流程
                sb.AppendLine($"  - **{Path.GetFileName(file)}** (Error parsing: {ex.Message})");
            }
        }

        return found ? sb.ToString() : "- **依赖信息**: 未发现 .csproj 文件\n";
    }

    /// <summary>
    /// 将通配符模式转换为预编译的正则表达式
    /// </summary>
    /// <param name="patterns">包含规则列表</param>
    /// <returns>包含正则对象与原始模式文本的元组列表</returns>
    private List<(Regex Regex, string Pattern)> PrepareIncludeRegexes(List<string> patterns)
    {
        var result = new List<(Regex Regex, string Pattern)>();
        if (patterns == null || patterns.Count == 0) return result; // <--- 原因：使用 Count 属性比 Any() 扩展方法在 List 上性能更优

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            // 预处理路径分隔符
            string normalizedPattern = pattern.Replace('\\', '/');

            // 将通配符转换为正则表达式
            string regexPattern = Regex.Escape(normalizedPattern)
                .Replace(@"\*\*/", "(.+/)?")
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".");

            // 显式指定非回溯优化（如果 .NET 8 运行环境支持）或使用 Compiled
            // <--- 原因：正则预编译减少匹配时的解析开销
            var re = new Regex("^" + regexPattern + "$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

            result.Add((re, regexPattern));
        }

        return result;
    }

    /// <summary>
    /// 判断给定路径是否符合包含规则。
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <param name="includeRegexes">预编译 的正则规则列表</param>
    /// <param name="isDirectory">当前路径是否为目录</param>
    /// <returns>如果应包含则返回 true</returns>
    private bool IsPathIncluded(string relativePath, List<(Regex Regex, string Pattern)> includeRegexes,
        bool isDirectory)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath == ".") return true;
        if (includeRegexes == null || includeRegexes.Count == 0) return true;

        // 统一使用向前斜杠处理路径匹配 // <--- 原因
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        string fileName = Path.GetFileName(normalized);

        // 1. 直接匹配当前路径或文件名
        bool isMatch = includeRegexes.Any(r =>
            r.Regex.IsMatch(normalized) || (!string.IsNullOrEmpty(fileName) && r.Regex.IsMatch(fileName)));

        if (isMatch) return true;

        // 2. 如果是目录，只要规则中存在以该目录开头的规则，就需要进入该目录遍历 // <--- 原因
        if (isDirectory)
        {
            string dirAsPrefix = normalized + "/";
            return includeRegexes.Any(r =>
                r.Pattern.StartsWith(dirAsPrefix, StringComparison.OrdinalIgnoreCase) ||
                r.Pattern.Contains("/" + dirAsPrefix, StringComparison.OrdinalIgnoreCase) ||
                // 补充：处理通配符起始的情况
                (r.Pattern.StartsWith(".*") || r.Pattern.StartsWith("(.+/)?")));
        }

        return false;
    }

    /// <summary>
    /// 递归构建文件目录树。
    /// </summary>
    private async Task BuildTreeRecursiveAsync(string currentPath, string rootPath, string indent, TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes)
    {
        // [Modified] 使用静态 DefaultEnumOptions
        var dirs = Directory.EnumerateDirectories(currentPath, "*", DefaultEnumOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        var files = Directory.EnumerateFiles(currentPath, "*", DefaultEnumOptions)
            .Where(f => !ExcludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f).ToList();

        var allItems = dirs.Concat(files)
            .Select(path => new { Path = path, IsDir = Directory.Exists(path) })
            .Where(x => IsPathIncluded(GetRelativePath(x.Path, rootPath), includeRegexes, x.IsDir))
            .ToList();

        for (int i = 0; i < allItems.Count; i++)
        {
            try
            {
                bool isLast = (i == allItems.Count - 1);
                var item = allItems[i];
                string name = Path.GetFileName(item.Path);

                string marker = !item.IsDir && (name.Equals("Program.cs") || name.Equals("App.xaml.cs") ||
                                                name.Equals("Startup.cs") || name.Equals("main.py"))
                    ? " [Entry Point]"
                    : "";

                await tw.WriteLineAsync($"{indent}{(isLast ? "└── " : "├── ")}{name}{(item.IsDir ? "/" : "")}{marker}");

                if (item.IsDir)
                {
                    await BuildTreeRecursiveAsync(item.Path, rootPath, indent + (isLast ? "    " : "│   "), tw,
                        includeRegexes);
                }
            }
            catch (UnauthorizedAccessException)
            {
                await tw.WriteLineAsync($"{indent}└── [Access Denied]");
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="FileCount"></param>
    /// <param name="LineCount"></param>
    private record ProjectStats(int FileCount, long LineCount);

    /// <summary>
    /// 处理目录并统计代码行数，同时写入文件内容。
    /// </summary>
    private async Task<ProjectStats> ProcessDirectoryWithStatsAsync(string currentPath, string rootPath, TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes, string taskMode)
    {
        int fileCount = 0;
        long totalLines = 0;

        // [Modified] 使用静态 DefaultEnumOptions
        var files = Directory.EnumerateFiles(currentPath, "*", DefaultEnumOptions)
            .Where(f => !ExcludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .Where(path => IsPathIncluded(GetRelativePath(path, rootPath), includeRegexes, false))
            .OrderBy(f => f);

        foreach (var file in files)
        {
            string relPath = GetRelativePath(file, rootPath);
            string extension = Path.GetExtension(file).ToLowerInvariant();

            await tw.WriteLineAsync($"\n[FILE: {relPath}]");

            try
            {
                string content = await File.ReadAllTextAsync(file);

                if (IsConfigFile(file))
                {
                    content = SanitizeSensitiveInfo(content);
                }

                if (taskMode != "explain")
                {
                    content = StripComments(content, extension);
                }

                string fence = content.Contains("```") ? "~~~~" : "```";
                await tw.WriteLineAsync($"{fence}{GetFileExtension(file)}");

                using var reader = new StringReader(content);
                int currentFileLine = 1;
                while (await reader.ReadLineAsync() is { } line)
                {
                    await tw.WriteAsync(currentFileLine.ToString());
                    await tw.WriteAsync("|");
                    await tw.WriteLineAsync(line.TrimEnd());

                    currentFileLine++;
                    totalLines++;
                }

                await tw.WriteLineAsync(fence);
                fileCount++;
                Console.WriteLine($"[写入成功] {relPath}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await tw.WriteLineAsync($"[Error skipping file: {ex.Message}]");
            }
        }

        var directories = Directory.EnumerateDirectories(currentPath, "*", DefaultEnumOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d);

        foreach (var directory in directories)
        {
            if (IsPathIncluded(GetRelativePath(directory, rootPath), includeRegexes, true))
            {
                var subStats = await ProcessDirectoryWithStatsAsync(directory, rootPath, tw, includeRegexes, taskMode);
                fileCount += subStats.FileCount;
                totalLines += subStats.LineCount;
            }
        }

        return new ProjectStats(fileCount, totalLines);
    }

    /// <summary>
    /// 去除代码中的注释（支持 C#, JS, TS, JSON）。
    /// </summary>

    private string StripComments(string content, string extension)
    {
        if (extension != ".cs" && extension != ".js" && extension != ".ts" && extension != ".json")
            return content;

        // [Modified] 调用编译时生成的正则方法
        return CommentStripRegex().Replace(content, m =>
        {
            // 如果是字符串字面量，保留原样
            if (m.Groups[1].Success) return m.Groups[1].Value;

            // [Modified] 仅保留换行符以保持行号一致，使用 LINQ 替代内部 Regex，减少分配
            // 原逻辑: return Regex.Replace(m.Value, @"[^\r\n]", "");
            return string.Concat(m.Value.Where(c => c == '\r' || c == '\n'));
        });
    }

    /// <summary>
    /// 使用 Source Generator 在编译时生成正则表达式，避免运行时解析开销。
    /// </summary>
    [GeneratedRegex(@"(@""(?:[^""]|"""")*""|""(?:\\.|[^\\""])*""|'(?:\\.|[^\\'])*')|//.*|/\*[\s\S]*?\*/", RegexOptions.Multiline)]
    private static partial Regex CommentStripRegex();
    
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
    /// 对配置文件内容进行脱敏处理，移除密钥、连接字符串等敏感数据
    /// </summary>
    /// <param name="content">原始文件内容</param>
    /// <returns>脱敏后的内容</returns>
    private string SanitizeSensitiveInfo(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        // 性能优化：直接使用静态预编译正则的 Replace 方法
        // <--- 原因：Regex.Replace 内部已包含匹配检查，无需外部重复调用 IsMatch
        foreach (var (pattern, replacement) in SensitivePatterns)
        {
            content = pattern.Replace(content, replacement);
        }

        return content;
    }

    /// <summary>
    /// 获取文件的 Markdown 语言标识符。增加了对 .NET 常见文件类型的支持。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>Markdown 语言关键字</returns>
    private string GetFileExtension(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" or ".razor" => "csharp", // 增加 Razor 支持 // <--- 原因
            ".json" or ".settings" or ".dev" => "json",
            ".xml" or ".csproj" or ".targets" or ".props" or ".config" => "xml", // 增加 MSBuild 相关支持 // <--- 原因
            ".md" => "markdown",
            ".js" or ".ts" => "javascript",
            ".sql" => "sql",
            ".yaml" or ".yml" => "yaml",
            _ => "text"
        };
    }
}