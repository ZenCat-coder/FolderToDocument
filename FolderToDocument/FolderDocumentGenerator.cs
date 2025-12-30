using System.Text;
using System.Text.RegularExpressions;

namespace FolderToDocument;

/// <summary>
/// 文件夹文档生成器核心类。
/// </summary>
public class FolderDocumentGenerator
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
        
        // <--- 原因: 根据 taskMode 切换角色，初学者模式需要更具教学性质的角色
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

        // <--- 原因: 新增 explain 模式的指令集，重点在于逻辑流程解释而非性能优化
        if (taskMode == "debug")
        {
            await sw.WriteLineAsync("## MODE: CRITICAL_DEBUG_REPAIR");
            await sw.WriteLineAsync("- TASK_1: Analyze code and pinpoint the root cause of potential runtime exceptions.");
            await sw.WriteLineAsync("- TASK_2: Provide a thread-safe, memory-efficient fix.");
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
        await sw.WriteLineAsync("- RULE_8: (Debug Only) Explain why the previous logic failed.");
        await sw.WriteLineAsync("- RULE_9: CATEGORIZED OUTPUT: Group findings into SECURITY, PERFORMANCE, LOGIC, ARCHITECTURE.");
        await sw.WriteLineAsync("- RULE_10: You MUST answer in Chinese.");
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
        // <--- 原因: 传递 taskMode 到底层处理函数，以便区分是否保留注释
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
    /// 解析项目元数据（Framework 版本及依赖包）。
    /// 优化点：增强了对多框架项目 (TargetFrameworks) 及包版本属性的提取稳定性。
    /// </summary>
    /// <param name="rootPath">项目根路径</param>
    /// <returns>项目元数据 Markdown 片段</returns>
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

            string content = await File.ReadAllTextAsync(file);

            // 增强正则：同时兼容 TargetFramework 和 TargetFrameworks 标签 // <--- 原因: 适应 .NET 现代项目的多目标框架配置
            var frameworkMatch = Regex.Match(content,
                @"<(?:TargetFramework|TargetFrameworks)\b[^>]*>(?<fw>.*?)</(?:TargetFramework|TargetFrameworks)>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string framework = frameworkMatch.Groups["fw"].Value;
            sb.AppendLine($"  - **{Path.GetFileName(file)}** (Framework: `{framework}`)");

            // 增强正则：支持 PackageReference 属性分布在多行或使用双引号/单引号的情形 // <--- 原因: 提高对不同代码格式风格的兼容性
            var packages = Regex.Matches(content,
                @"<PackageReference\s+[^>]*Include=[""'](?<name>.*?)[""'](?:\s+Version=[""'](?<ver>.*?)[""'])?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach (Match p in packages)
            {
                string name = p.Groups["name"].Value;
                string ver = p.Groups["ver"].Success ? p.Groups["ver"].Value : "Latest";
                sb.AppendLine($"    - `{name}` (v{ver})");
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
    /// 异步递归构建视觉化目录树，确保在处理大型文件系统时不阻塞线程池。
    /// </summary>
    /// <param name="currentPath">当前遍历路径</param>
    /// <param name="rootPath">根路径</param>
    /// <param name="indent">缩进字符串</param>
    /// <param name="tw">输出流</param>
    /// <param name="includeRegexes">包含规则</param>
    /// <returns>异步任务</returns>
    private async Task BuildTreeRecursiveAsync(string currentPath, string rootPath, string indent, TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes)
    {
        var enumerationOptions = new EnumerationOptions
            { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false };

        var dirs = Directory.EnumerateDirectories(currentPath, "*", enumerationOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        var files = Directory.EnumerateFiles(currentPath, "*", enumerationOptions)
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

                // 修改为异步写入，保持 IO 管道的一致性 // <--- 原因
                await tw.WriteLineAsync($"{indent}{(isLast ? "└── " : "├── ")}{name}{(item.IsDir ? "/" : "")}{marker}");

                if (item.IsDir)
                {
                    // 递归调用同步转异步 // <--- 原因
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
    /// 递归处理目录并提取源码内容，支持流式脱敏与统计
    /// </summary>
    /// <param name="currentPath">当前路径</param>
    /// <param name="rootPath">项目根路径</param>
    /// <param name="tw">输出流写入器</param>
    /// <param name="includeRegexes">预编译的过滤规则</param>
    /// <returns>项目统计数据（文件数、行数）</returns>
    private async Task<ProjectStats> ProcessDirectoryWithStatsAsync(string currentPath, string rootPath, TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes, string taskMode)
    {
        int fileCount = 0;
        long totalLines = 0;

        var enumOptions = new EnumerationOptions
            { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false };

        var files = Directory.EnumerateFiles(currentPath, "*", enumOptions)
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

                // <--- 原因: 对于初学者模式，保留注释是极好的学习资源。如果是优化或Debug模式，则清理注释以节省 Token。
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

        var directories = Directory.EnumerateDirectories(currentPath, "*", enumOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d);

        foreach (var directory in directories)
        {
            if (IsPathIncluded(GetRelativePath(directory, rootPath), includeRegexes, true))
            {
                // <--- 原因: 递归调用时继续传递 taskMode
                var subStats = await ProcessDirectoryWithStatsAsync(directory, rootPath, tw, includeRegexes, taskMode);
                fileCount += subStats.FileCount;
                totalLines += subStats.LineCount;
            }
        }

        return new ProjectStats(fileCount, totalLines);
    }

    /// <summary>
    /// 清理源码中的注释，并保持行号对齐。
    /// </summary>
    /// <param name="content">原始内容</param>
    /// <param name="extension">扩展名</param>
    /// <returns>清理后的内容</returns>
    private string StripComments(string content, string extension)
    {
        if (extension != ".cs" && extension != ".js" && extension != ".ts" && extension != ".json")
            return content;

        // 通过正则捕获组 1 保护字符串内容，防止误删 URL 中的 //
        string pattern = @"(@""(?:[^""]|"""")*""|""(?:\\.|[^\\""])*""|'(?:\\.|[^\\'])*')|//.*|/\*[\s\S]*?\*/";

        return Regex.Replace(content, pattern, m =>
        {
            if (m.Groups[1].Success) return m.Groups[1].Value;

            // 将非换行符替换为空白，从而保持行号逻辑绝对一致 // <--- 原因：确保清理后的代码行号与源码文件 100% 同步，这是 AI 准确定位的基石
            return Regex.Replace(m.Value, @"[^\r\n]", "");
        }, RegexOptions.Compiled | RegexOptions.Multiline);
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