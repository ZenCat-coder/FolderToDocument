using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis; // <--- Roslyn 核心
using Microsoft.CodeAnalysis.CSharp; // <--- C# 语法树
using Microsoft.CodeAnalysis.CSharp.Syntax; // <--- 语法节点类型

namespace FolderToDocument;

/// <summary>
/// 文件夹文档生成器核心类。
/// </summary>
public partial class FolderDocumentGenerator
{
    private static readonly FrozenSet<string> ExcludedFolders =
        new[]
            {
                "bin", "obj", ".vs", ".git", "node_modules", "packages", "Debug", "Release", ".idea", "dist", "build",
                "__pycache__", "Properties"
            }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".cache", ".user", ".suo",
        ".jpg", ".png", ".gif", ".ico", ".pdf", ".zip", ".rar", ".7z", ".map", ".bmp"
    };

    private static readonly EnumerationOptions DefaultEnumOptions = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = false
    };

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 敏感信息脱敏模式集合。
    /// </summary>
    private static readonly List<(Regex pattern, string replacement)> SensitivePatterns =
    [
        (new Regex(@"ConnectionString\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout),
            "ConnectionString=\"***\""),

        (new Regex(@"(?:AppKey|Secret|Password|Token|Pwd|ApiKey|RegCode)\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout),
            "Property=\"***\""),

        // <--- 原因: 弱化边界检查，减少回溯。如果是配置文件，通常这些值在引号内。
        (new Regex(@"[""'][0-9a-fA-F]{32,}[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout),
            "\"***HEX_SECRET***\""),

        // <--- 原因: 简化邮箱匹配逻辑，仅在配置类文件中做基础脱敏，降低复杂度
        (new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout),
            "user@example.com")
    ];

    /// <summary>
    /// 执行文档生成的主逻辑
    /// </summary>
    /// <param name="rootPath">待分析的项目根路径</param>
    /// <param name="outputPath">输出 Markdown 的路径（可选）</param>
    /// <param name="includedPatterns">包含文件的通配符规则（可选）</param>
    /// <param name="taskMode">AI 任务模式（debug/optimize/explain/skeleton）</param>
    /// <param name="customRequirements">自定义需求描述列表</param>
    /// <param name="excludedClasses">需要从输出中排除的类名列表（所有模式均生效）</param>
    /// <param name="preservedMethods"></param>
    /// <param name="excludedFolders"></param>
    /// <returns>生成的文档物理路径</returns>
    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null,
        List<string> includedPatterns = null, string taskMode = "optimize",
        List<string> customRequirements = null,
        List<string> excludedClasses = null,
        List<string> preservedMethods = null,
        List<string> excludedFolders = null) // <--- 原因: 新增类排除参数，保持默认值以确保对已有调用方的向后兼容
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Source directory not found: {rootPath}");

        var currentRegexes = PrepareIncludeRegexes(includedPatterns);

        rootPath = Path.GetFullPath(rootPath);

        string projectName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(projectName)) projectName = "Project";

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

        var streamOptions = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous
        };

        await using (var sw = new StreamWriter(outputPath, Encoding.UTF8, streamOptions))
        {
            await sw.WriteLineAsync("<TaskDefinition>");

            if (taskMode == "explain")
            {
                await sw.WriteLineAsync("## ROLE: Senior Technical Educator");
                await sw.WriteLineAsync(
                    "## EXPERTISE: C# Programming, Logic Explanation, Software Engineering Fundamentals");
            }
            else
            {
                await sw.WriteLineAsync("## ROLE: Senior Software Architect");
                await sw.WriteLineAsync(
                    "## EXPERTISE: .NET 8 and later, High-Performance Systems, Secure Coding, Clean Architecture");
            }

            await sw.WriteLineAsync("## THOUGHT_PROCESS: Mandatory Chain-of-Thought");
            await sw.WriteLineAsync("- STEP_1: Identify all potential side effects on existing logic.");
            await sw.WriteLineAsync("- STEP_2: Verify API compatibility (method signatures).");
            await sw.WriteLineAsync("- STEP_3: Explicitly check for null-references and exception safety.");
            await sw.WriteLineAsync("- STEP_4: Confirm .NET 8 best practices (Span, Memory, Task).");
            await sw.WriteLineAsync(
                "- STEP_5: GLOBAL PATTERN SCAN: Search the entire provided context for identical or similar logic patterns and apply the same optimization to ALL of them to ensure consistency.");

            if (taskMode == "debug")
            {
                await sw.WriteLineAsync("## MODE: CRITICAL_DEBUG_REPAIR");
                await sw.WriteLineAsync(
                    "- TASK_1: Analyze code and pinpoint the root cause of potential runtime exceptions.");
                await sw.WriteLineAsync("- TASK_2: Provide a thread-safe, memory-efficient fix.");
                await sw.WriteLineAsync("- TASK_3: Explain why the previous logic failed.");
            }
            else if (taskMode == "explain")
            {
                await sw.WriteLineAsync("## MODE: BEGINNER_CODE_WALKTHROUGH");
                await sw.WriteLineAsync("- TASK_1: Explain the high-level workflow of the code in simple terms.");
                await sw.WriteLineAsync(
                    "- TASK_2: Break down complex methods and explain the purpose of key variables.");
                await sw.WriteLineAsync("- TASK_3: Highlight common C# patterns used (e.g., async/await, Linq).");
            }
            else if (taskMode == "skeleton")
            {
                await sw.WriteLineAsync("## MODE: SKELETON_ARCHITECTURE_REVIEW");
                await sw.WriteLineAsync(
                    "- TASK_1: Analyze architecture and design patterns solely from class/method signatures.");
                await sw.WriteLineAsync(
                    "- TASK_2: Identify SOLID violations, excessive coupling, and naming inconsistencies.");
                await sw.WriteLineAsync("- TASK_3: Suggest structural refactoring based on the skeleton overview.");
                await sw.WriteLineAsync(
                    "- NOTE: Method bodies have been stripped to reduce token usage. Focus on structure, not implementation.");
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
            await sw.WriteLineAsync(
                "- RULE_2: You MUST provide the ENTIRE method or logic block. DO NOT use snippets (e.g., `...`) or partial updates.");
            await sw.WriteLineAsync("- RULE_3: The [Modified] code block MUST NOT contain line numbers.");
            await sw.WriteLineAsync(
                "- RULE_4: Only functional logic changes require the (// <--- 原因) comment. DO NOT add this comment to justify why you DID NOT change the code.");
            await sw.WriteLineAsync(
                "- RULE_5: [STRICT] If the executable logic of a method is not changed, DO NOT output it. Adding, removing, or modifying comments/XML docs ALONE does not count as a change.");
            await sw.WriteLineAsync(
                "- RULE_6: CONSISTENCY ENFORCEMENT: If an optimization or fix applies to multiple locations (even in different methods or files), you MUST include ALL affected blocks. DO NOT optimize one and leave the others in their original state.");
            await sw.WriteLineAsync(
                "- RULE_7: CATEGORIZED OUTPUT: Group findings into SECURITY, PERFORMANCE, LOGIC, ARCHITECTURE.");
            await sw.WriteLineAsync("- RULE_8: You MUST answer in Chinese.");
            await sw.WriteLineAsync(
                "- RULE_9: XML documentation (/// <summary>) should ONLY be added/updated if the method's logic was actually modified.");
            await sw.WriteLineAsync(
                "- RULE_10: ABSOLUTELY FORBIDDEN to output a method just to provide an 'analysis' or 'confirmation' if no code was improved.");
            await sw.WriteLineAsync("</OutputStrictConstraint>\n\n---\n");

            await sw.WriteLineAsync($"# {projectName} 项目文档");
            await sw.WriteLineAsync($"> 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await sw.WriteLineAsync($"- **项目根目录**: `{rootPath}`");
            await sw.WriteAsync(projectMetadata);
            await sw.WriteLineAsync();

            Console.WriteLine("[2/5] 正在构建目录树...");
            await sw.WriteLineAsync("## 1. 项目目录结构\n```text");
            await sw.WriteLineAsync($"{projectName}/");
            await BuildTreeRecursiveAsync(rootPath, rootPath, "", sw, currentRegexes, excludedFolders);
            await sw.WriteLineAsync("```\n---\n");

            Console.WriteLine("[3/5] 正在处理源码...");

            var stats = await ProcessDirectoryWithStatsAsync(rootPath, rootPath, sw, currentRegexes, taskMode,
                excludedClasses, preservedMethods, excludedFolders);
            await sw.WriteLineAsync("\n<ImportantReminder>");
            await sw.WriteLineAsync("System Context Loaded. .NET 8 Strict Mode.");
            await sw.WriteLineAsync(
                "Consistency Check: I will ensure that every identified optimization pattern is applied globally across all provided methods.");
            await sw.WriteLineAsync(
                "No Half-Measures: If I fix a performance/security issue in one block, I will scan and fix it in all related blocks.");
            await sw.WriteLineAsync("</ImportantReminder>\n\n---");

            await sw.WriteLineAsync("## 3. 项目规模 with 统计");
            await sw.WriteLineAsync($"- **文件总数**: {stats.FileCount}");
            await sw.WriteLineAsync($"- **代码总行数**: {stats.LineCount}");
            await sw.WriteLineAsync($"- **安全状态**: 已自动执行正则脱敏");
        }

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

        // 使用更精确的路径分隔符检查逻辑，防止误伤
        var csprojFiles = Directory.EnumerateFiles(rootPath, "*.csproj", enumerationOptions)
            .Where(f => !ExcludedFolders.Any(ef =>
                f.Split(Path.DirectorySeparatorChar)
                    .Contains(ef, StringComparer.OrdinalIgnoreCase))); // <--- 原因：精确匹配文件夹段，避免子字符串误匹配

        var sb = new StringBuilder();
        bool found = false;

        foreach (var file in csprojFiles)
        {
            if (!found)
            {
                sb.AppendLine("- **项目技术栈与依赖库**:");
                found = true;
            }

            try
            {
                await using var stream =
                    new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                        true); // <--- 原因：配置异步 FileStream
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

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
                sb.AppendLine($"  - **{Path.GetFileName(file)}** (解析失败: {ex.Message})"); // <--- 原因：更友好的错误记录
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
        List<(Regex Regex, string Pattern)> includeRegexes,
        List<string> excludedFolders = null) // <--- 原因: 新增参数，目录树渲染时过滤用户指定文件夹
    {
        var dirs = Directory.EnumerateDirectories(currentPath, "*", DefaultEnumOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .Where(d => excludedFolders == null ||
                        !excludedFolders.Contains(Path.GetFileName(d),
                            StringComparer.OrdinalIgnoreCase)) // <--- 原因: 在静态黑名单之后追加用户动态黑名单过滤
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
                        includeRegexes, excludedFolders); // <--- 原因: 递归时向下传递 excludedFolders
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
    /// 支持在所有模式下按类名过滤 C# 文件中的指定类。
    /// </summary>
    private async Task<ProjectStats> ProcessDirectoryWithStatsAsync(string currentPath, string rootPath, TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes, string taskMode,
        List<string> excludedClasses = null,
        List<string> preservedMethods = null,
        List<string> excludedFolders = null)
    {
        int fileCount = 0;
        long totalLines = 0;

        var files = Directory.EnumerateFiles(currentPath, "*", DefaultEnumOptions)
            .Where(f => !ExcludedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) // <--- 原因：统一字符串规范
            .Where(path => IsPathIncluded(GetRelativePath(path, rootPath), includeRegexes, false))
            .OrderBy(f => f);

        foreach (var file in files)
        {
            string relPath = GetRelativePath(file, rootPath);
            string extension = Path.GetExtension(file).ToLowerInvariant();

            await tw.WriteLineAsync($"\n[FILE: {relPath}]");

            try
            {
                string preloadedSource = null;
                bool allClassesExcluded = false;

                if (extension == ".cs" && !IsConfigFile(file) && excludedClasses is { Count: > 0 })
                {
                    string rawSource = await File.ReadAllTextAsync(file, Encoding.UTF8);
                    (preloadedSource, allClassesExcluded) = await RemoveExcludedClassesAsync(rawSource, excludedClasses);
                }

                if (allClassesExcluded)
                {
                    await tw.WriteLineAsync("// [已排除] 该文件中所有指定的类均已被过滤，不予展示。");
                    fileCount++;
                    Console.WriteLine($"[已排除] {relPath}");
                    continue;
                }

                if (!IsConfigFile(file) && taskMode == "explain")
                {
                    await tw.WriteLineAsync($"```{GetFileExtension(file)}");

                    if (preloadedSource != null)
                    {
                        using var sr = new StringReader(preloadedSource);
                        totalLines += await WriteOptimizedContentAsync(sr, tw); // <--- 原因：重构复用流式去空行管线
                    }
                    else
                    {
                        await using var fs = new FileStream(file, new FileStreamOptions
                        {
                            Mode = FileMode.Open,
                            Access = FileAccess.Read,
                            Share = FileShare.Read,
                            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                        });

                        using var streamReader = new StreamReader(fs, Encoding.UTF8);
                        totalLines += await WriteOptimizedContentAsync(streamReader, tw); // <--- 原因：重构复用流式去空行管线
                    }

                    await tw.WriteLineAsync("```");
                }
                else if (taskMode == "skeleton" && extension == ".cs" && !IsConfigFile(file))
                {
                    string sourceForSkeleton = preloadedSource ?? await File.ReadAllTextAsync(file, Encoding.UTF8);
                    string skeleton = await ExtractCSharpSkeletonAsync(sourceForSkeleton, preservedMethods);

                    string fence = skeleton.Contains("```") ? "~~~~" : "```";
                    await tw.WriteLineAsync($"{fence}csharp");

                    using var sr = new StringReader(skeleton);
                    totalLines += await WriteOptimizedContentAsync(sr, tw); // <--- 原因：重构复用流式去空行管线

                    await tw.WriteLineAsync(fence);
                }
                else
                {
                    string content = preloadedSource ?? await File.ReadAllTextAsync(file, Encoding.UTF8);
                    if (IsConfigFile(file)) content = SanitizeSensitiveInfo(content);
                    if (taskMode != "explain") content = StripComments(content, extension);

                    string fence = content.Contains("```") ? "~~~~" : "```";
                    await tw.WriteLineAsync($"{fence}{GetFileExtension(file)}");

                    using var sr = new StringReader(content);
                    totalLines += await WriteOptimizedContentAsync(sr, tw); // <--- 原因：重构复用流式去空行管线

                    await tw.WriteLineAsync(fence);
                }

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
            .Where(d => excludedFolders == null || !excludedFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            .OrderBy(d => d);

        foreach (var directory in directories)
        {
            if (IsPathIncluded(GetRelativePath(directory, rootPath), includeRegexes, true))
            {
                var subStats = await ProcessDirectoryWithStatsAsync(directory, rootPath, tw, includeRegexes, taskMode,
                    excludedClasses, preservedMethods, excludedFolders);
                fileCount += subStats.FileCount;
                totalLines += subStats.LineCount;
            }
        }

        return new ProjectStats(fileCount, totalLines);
    }

    // <--- 原因：新增的高性能流拦截器，使用 ReadOnlyMemory<char> 直接干预，彻底消灭末尾空格和超过一次的空白行。
    private async Task<long> WriteOptimizedContentAsync(TextReader sr, TextWriter tw)
    {
        long lines = 0;
        bool previousLineWasEmpty = false;

        while (await sr.ReadLineAsync() is { } line)
        {
            var trimmedLine = line.AsMemory().TrimEnd();
            bool isLineEmpty = trimmedLine.Length == 0;

            if (isLineEmpty)
            {
                if (previousLineWasEmpty) continue; // 直接截断连续空白行产生
                previousLineWasEmpty = true;
            }
            else
            {
                previousLineWasEmpty = false;
            }

            await tw.WriteLineAsync(trimmedLine);
            lines++;
        }
        return lines;
    }
    
    /// <summary>
    /// 将传入的 C# 源码字符串转为方法体已剥离的骨架字符串。
    /// 签名改为接收已读取（且经过类排除处理）的 source，避免重复 IO。
    /// </summary>
    private static async Task<string> ExtractCSharpSkeletonAsync(
        string source,
        IReadOnlyCollection<string> preservedMethods = null) // <--- 原因: 将保留列表传入 SkeletonRewriter，实现选择性保留
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();
        var rewriter = new SkeletonRewriter(preservedMethods);
        var skeletonRoot = rewriter.Visit(root);
        return skeletonRoot.ToFullString();
    }

    /// <summary>
    /// 解析 C# 源码，移除名称在 excludedClasses 列表中的类声明，并返回处理后的源码。
    /// </summary>
    /// <param name="source">原始 C# 源码字符串</param>
    /// <param name="excludedClasses">需要排除的类名集合</param>
    /// <returns>过滤后的源码，以及是否所有类均已被排除的标志</returns>
    private static async Task<(string FilteredSource, bool AllExcluded)> RemoveExcludedClassesAsync(
        string source, IReadOnlyCollection<string> excludedClasses)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();

        // 统计原始源码中有多少个类名命中了排除列表（避免把无类的文件误判为"全排除"）
        int originalMatchCount = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Count(c => excludedClasses.Contains(c.Identifier.ValueText)); // <--- 原因: 仅统计命中项，区分"无类文件"与"全排除文件"

        var rewriter = new ClassRemovalRewriter(excludedClasses);
        var newRoot = rewriter.Visit(root);
        string filtered = newRoot.ToFullString();

        int remainingClassCount = newRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Count();

        // 只有：原来有命中的类 && 现在一个类都不剩，才认为"全部排除"
        bool allExcluded =
            originalMatchCount > 0 &&
            remainingClassCount == 0; // <--- 原因: 双重条件，防止将本身就没有 class 的文件（如纯顶层语句的 Program.cs）误判为全排除

        return (filtered, allExcluded);
    }

    /// <summary>
    /// 去除代码中的注释（支持 C#, JS, TS, JSON）。
    /// </summary>
    private string StripComments(string content, string extension)
    {
        if (extension is not (".cs" or ".js" or ".ts" or ".json"))
            return content;

        return CommentStripRegex().Replace(content, m =>
        {
            if (m.Groups[1].Success) return m.Groups[1].Value;
            
            // <--- 原因：不再无意义地保留注释段落原有的换行符，从根源上将多行注释切底清空。
            return string.Empty;
        });
    }

    /// <summary>
    /// 使用 Source Generator 在编译时生成正则表达式，避免运行时解析开销。
    /// </summary>
    [GeneratedRegex(@"(@""(?:[^""]|"""")*""|""(?:\\.|[^\\""])*""|'(?:\\.|[^\\'])*')|//.*|/\*[\s\S]*?\*/",
        RegexOptions.Multiline)]
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

        string processed = content;

        foreach (var (pattern, replacement) in SensitivePatterns)
        {
            try
            {
                // <--- 原因：摒弃错误的 Split 处理，不再因为 \r\n 形成大量空白行，直接全局正则替换并节约内存分配。
                processed = pattern.Replace(processed, replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }
        }

        return processed;
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

    // [Original]
// private sealed class SkeletonRewriter : CSharpSyntaxRewriter
// {
//     private static string BuildBlockHint(BlockSyntax body)
//     {
//         ...（仅提取 calls / return / throws，共3类）
//     }
//     public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) { ... }
//     public override SyntaxNode VisitConstructorDeclaration(...) { ... }
//     public override SyntaxNode VisitDestructorDeclaration(...) { ... }
//     public override SyntaxNode VisitAccessorDeclaration(...) { ... }
//     public override SyntaxNode VisitOperatorDeclaration(...) { ... }
// }

    private sealed class SkeletonRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _preserved; // <--- 原因: 存储需要保留完整实现的标识符集合（类名/方法名/类名.方法名）
        private string _currentClassName; // <--- 原因: 追踪当前正在访问的类名，支持 "ClassName.MethodName" 精确匹配及嵌套类场景

        private static readonly HashSet<string> LinqOperatorNames = new(StringComparer.Ordinal)
        {
            "Where", "Select", "SelectMany", "GroupBy", "OrderBy", "OrderByDescending",
            "ThenBy", "Join", "Any", "All", "First", "FirstOrDefault", "Single",
            "SingleOrDefault", "Count", "Sum", "Min", "Max", "Distinct", "ToList",
            "ToArray", "ToDictionary", "ToFrozenSet", "Aggregate", "Skip", "Take",
            "Concat", "Zip", "Except", "Intersect", "Union"
        }; // <--- 原因: 编译期常量集合，避免每次调用 BuildBlockHint 时重建，O(1) 查找

        public SkeletonRewriter(IEnumerable<string> preservedMethods = null)
        {
            _preserved = preservedMethods != null
                ? new HashSet<string>(preservedMethods, StringComparer.Ordinal)
                : [];
        }

        // ── 判断当前节点是否应保留完整实现 ────────────────────────────────────
        private bool ShouldPreserve(string simpleName)
        {
            if (_preserved.Count == 0) return false;
            if (_preserved.Contains(simpleName)) return true; // 匹配裸方法名/类名
            if (_currentClassName != null &&
                _preserved.Contains(_currentClassName + "." + simpleName)) return true; // 匹配 ClassName.MethodName
            return false;
        }

        // ── BuildBlockHint：增强版，从 BlockSyntax 中提取7类语义线索 ───────────
        private static string BuildBlockHint(BlockSyntax body)
        {
            if (body == null || body.Statements.Count == 0) return "/* empty */";

            var parts = new List<string>(7);

            // 1. 调用的方法名（去重，取前6个）
            var calls = body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Parent is not ArgumentSyntax) // <--- 原因: 排除作为参数嵌套的调用（如 Math.Max(0, x + delta)），只保留顶层调用语句，减少噪音
                .Select(inv =>
                {
                    string receiver = inv.Expression.ToString();
                    string args = string.Join(", ", inv.ArgumentList.Arguments.Select(a => a.ToString())); // <--- 原因: 展开实参列表，让 AI 能看到传了哪些变量，而不只是方法名
                    string full = args.Length > 0 ? $"{receiver}({args})" : $"{receiver}()";

                    // 超长时截断参数部分，保留 receiver 可读性
                    return full.Length <= 80 ? full : $"{receiver}(...)"; // <--- 原因: 超过 80 字符时折叠参数，避免 hint 行过长影响 AI 解析
                })
                .Where(n => n.Length > 0 && !LinqOperatorNames.Contains( // 用方法名部分做 LINQ 过滤
                    n.Contains('(') ? n[..n.IndexOf('(')] .Split('.').Last() : n))
                .Distinct()
                .Take(6);
            var callStr = string.Join(", ", calls);
            if (callStr.Length > 0) parts.Add($"calls: {callStr}");

            // 2. LINQ 算子链（帮助 AI 理解数据转换意图） // <--- 原因: 新增分类，LINQ 链是骨架中信息损失最严重的部分
            var linqOps = body.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Select(m => m.Name.Identifier.ValueText)
                .Where(n => LinqOperatorNames.Contains(n))
                .Distinct()
                .Take(5);
            var linqStr = string.Join("→", linqOps);
            if (linqStr.Length > 0) parts.Add($"linq: {linqStr}");

            // 3. 实例化的类型（帮助 AI 理解依赖的对象图） // <--- 原因: new T() 是理解方法职责的关键，原版完全缺失
            var newTypes = body.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Select(o => o.Type.ToString())
                .Where(t => t.Length is > 0 and <= 35)
                .Distinct()
                .Take(3);
            var newStr = string.Join(", ", newTypes);
            if (newStr.Length > 0) parts.Add($"new: {newStr}");

            // 4. foreach 的迭代源（帮助 AI 理解遍历对象） // <--- 原因: 新增分类，遍历的是什么集合直接决定方法的数据处理逻辑
            var foreachSources = body.DescendantNodes()
                .OfType<ForEachStatementSyntax>()
                .Select(f => f.Expression.ToString().Trim())
                .Where(e => e.Length is > 0 and <= 30)
                .Distinct()
                .Take(2);
            var foreachStr = string.Join(", ", foreachSources);
            if (foreachStr.Length > 0) parts.Add($"foreach: {foreachStr}");

            // 5. 关键条件分支（仅保留短表达式，避免噪音） // <--- 原因: 新增分类，if 的判断条件往往是理解方法分支逻辑的核心
            var conditions = body.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Select(i => i.Condition.ToString().Trim())
                .Where(c => c.Length is > 0 and <= 45)
                .Distinct()
                .Take(2);
            var condStr = string.Join(" | ", conditions);
            if (condStr.Length > 0) parts.Add($"if: {condStr}");

            // 6. return 表达式
            var returns = body.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Where(r => r.Expression != null)
                .Select(r => r.Expression!.ToString().Trim())
                .Where(e => e.Length is > 0 and <= 60)
                .Distinct()
                .Take(2);
            var retStr = string.Join(" | ", returns);
            if (retStr.Length > 0) parts.Add($"→ {retStr}");

            // 7. throw 类型
            var throwTypes = body.DescendantNodes()
                .Select<SyntaxNode, string>(n => n switch
                {
                    ThrowStatementSyntax ts =>
                        (ts.Expression as ObjectCreationExpressionSyntax)?.Type.ToString() ?? "",
                    ThrowExpressionSyntax te =>
                        (te.Expression as ObjectCreationExpressionSyntax)?.Type.ToString() ?? "",
                    _ => ""
                })
                .Where(t => t.Length > 0)
                .Distinct()
                .Take(2);
            var throwStr = string.Join(", ", throwTypes);
            if (throwStr.Length > 0) parts.Add($"throws: {throwStr}");

            return parts.Count > 0 ? $"/* {string.Join(" | ", parts)} */" : "/* ... */";
        }

        private static string BuildExpressionHint(ArrowExpressionClauseSyntax arrow)
        {
            var expr = arrow.Expression.ToString().Trim().Replace("*/", "*\\/");
            if (expr.Length > 120) expr = expr[..120] + "...";
            return $"/* => {expr} */";
        }

        private static BlockSyntax WrapInStubBlock(string hint) =>
            SyntaxFactory.Block()
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                        .WithTrailingTrivia(SyntaxFactory.TriviaList(
                            SyntaxFactory.Space,
                            SyntaxFactory.Comment(hint),
                            SyntaxFactory.Space)))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        private static BlockSyntax StubFromBlock(BlockSyntax body) => WrapInStubBlock(BuildBlockHint(body));

        private static BlockSyntax StubFromArrow(ArrowExpressionClauseSyntax a) =>
            WrapInStubBlock(BuildExpressionHint(a));

        // ── 追踪当前类名，支持嵌套类（入栈/出栈模式） ───────────────────────────
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText))
                return node; // <--- 原因: 类名命中保留列表，整个类节点原样返回，不递归重写任何成员

            var prev = _currentClassName;
            _currentClassName = node.Identifier.ValueText; // <--- 原因: 入栈，使子节点的 ShouldPreserve 能拼接 ClassName.MethodName
            var result = base.VisitClassDeclaration(node);
            _currentClassName = prev; // <--- 原因: 出栈，正确处理嵌套类场景
            return result;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText)) return node; // <--- 原因: 方法名命中，保留完整实现

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText)) return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitConstructorDeclaration(node);
        }

        public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText)) return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitDestructorDeclaration(node);
        }

        public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Keyword.ValueText))
                return
                    node; // <--- 原因: AccessorDeclarationSyntax 无 Identifier 属性，属性访问器以 Keyword（get/set/init）标识，必须改用 Keyword.ValueText

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitAccessorDeclaration(node);
        }

        public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.OperatorToken.ValueText))
                return node; // <--- 原因: OperatorDeclarationSyntax 无 Identifier 属性，运算符以 OperatorToken 标识（如 +、-、==）

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitOperatorDeclaration(node);
        }
    }
    

    [GeneratedRegex(@"\n{3,}", RegexOptions.None)]
    private static partial Regex MultipleBlankLinesRegex();

    /// <summary>
    /// Roslyn 语法重写器：按类名从语法树中移除指定的类声明节点。
    /// 在 Visit 中返回 null 即代表删除该节点（Roslyn 标准做法）。
    /// </summary>
    private sealed class ClassRemovalRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _excludedNames; // <--- 原因: 使用 HashSet 保证 O(1) 的类名查找

        public ClassRemovalRewriter(IEnumerable<string> excludedNames)
        {
            _excludedNames = new HashSet<string>(excludedNames, StringComparer.Ordinal);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (_excludedNames.Contains(node.Identifier.ValueText))
                return null; // <--- 原因: Roslyn Rewriter 中返回 null 等价于从父节点的子列表中删除该节点
            return base.VisitClassDeclaration(node); // 未命中则递归处理其内部（支持嵌套类场景）
        }
    }
}