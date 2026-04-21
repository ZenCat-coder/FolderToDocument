using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FolderToDocument.Interfaces;

namespace FolderToDocument;

public class FolderDocumentGenerator
{
    private static readonly FrozenSet<string> ExcludedFolders = new[]
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages", "Debug", "Release", ".idea", "dist", "build",
        "__pycache__", "Properties"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    

    private readonly IFileSystemService _fileSystem;
    private readonly ICodeAnalysisService _codeAnalysis;
    private readonly IContentProcessor _contentProcessor;
    private readonly IOutputStrategySelector _strategySelector;
    private readonly IDirectoryTraversalService _directoryTraversal;

    /// <summary>
    /// 构造方法
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="codeAnalysis"></param>
    /// <param name="contentProcessor"></param>
    /// <param name="strategySelector"></param>
    /// <param name="directoryTraversal"></param>
    private FolderDocumentGenerator(
        IFileSystemService fileSystem,
        ICodeAnalysisService codeAnalysis,
        IContentProcessor contentProcessor,
        IOutputStrategySelector strategySelector,
        IDirectoryTraversalService directoryTraversal)
    {
        _fileSystem = fileSystem;
        _codeAnalysis = codeAnalysis;
        _contentProcessor = contentProcessor;
        _strategySelector = strategySelector;
        _directoryTraversal = directoryTraversal;
    }

    // 默认构造函数，保持与原有调用兼容
    public FolderDocumentGenerator() : this(
        new Services.FileSystemService(),
        new Services.CodeAnalysisService(),
        new Services.ContentProcessor(),
        new Services.OutputStrategySelector(),
        new Services.DirectoryTraversalService())
    {
    }

    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null,
        List<string> includedPatterns = null, string taskMode = "optimize",
        List<string> customRequirements = null,
        List<string> excludedClasses = null,
        List<string> preservedMethods = null,
        List<string> excludedFolders = null,
        List<string> entryClasses = null,
        int entryClassesMaxDepth = -1)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        if (!_fileSystem.DirectoryExists(rootPath))
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
            Directory.CreateDirectory(outputDir);

        Console.WriteLine("[1/5] 正在解析项目元数据...");
        string projectMetadata = await GetProjectMetadataAsync(rootPath);

        HashSet<string> reachableClasses = null;
        if (entryClasses is { Count: > 0 })
        {
            Console.WriteLine("[1.5/5] 正在构建类引用图，分析入口类的可达范围...");
            var graph = await _codeAnalysis.BuildClassReferenceGraphAsync(rootPath, excludedFolders, _fileSystem);
            reachableClasses = ResolveReachableClasses(graph, entryClasses, entryClassesMaxDepth);
            string depthDesc = entryClassesMaxDepth < 0 ? "无限制" : $"最多 {entryClassesMaxDepth} 轮";
            string preview = string.Join(", ", reachableClasses.Take(10));
            string more = reachableClasses.Count > 10
                ? $"... 等共 {reachableClasses.Count} 个"
                : $"共 {reachableClasses.Count} 个";
            Console.WriteLine($"[1.5/5] 可达类集合（深度={depthDesc}）：{preview} {more}");
        }

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

            // 写入 ROLE 和 EXPERTISE（与原来相同）
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
            
            await sw.WriteLineAsync("## MANDATORY_RULES:");
            await sw.WriteLineAsync("- RULE_C: New command classes must inject dependencies via constructor, and must not use the ServiceLocator pattern.");
            
            // 写入 MODE 和 TASK（与原来相同）
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
                await sw.WriteLineAsync("> 3. **业务需求**：");
                foreach (var req in customRequirements) await sw.WriteLineAsync($">    - {req}");
            }

            await sw.WriteLineAsync("<OutputStrictConstraint>");
            await sw.WriteLineAsync("- RULE_1: You MUST output every change using a code block format.");
            await sw.WriteLineAsync("- RULE_2: You MUST answer in Chinese.");
            await sw.WriteLineAsync("</OutputStrictConstraint>\n\n---\n");

            await sw.WriteLineAsync($"# {projectName} 项目文档");
            await sw.WriteLineAsync($"> 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await sw.WriteLineAsync($"- **项目根目录**: `{rootPath}`");
            await sw.WriteAsync(projectMetadata);
            await sw.WriteLineAsync();

            if (reachableClasses != null)
            {
                await sw.WriteLineAsync(
                    $"> ℹ️ **类引用过滤模式**：以 `{string.Join(", ", entryClasses)}` 为入口，仅保留 {reachableClasses.Count} 个可达类的相关代码。\n");
            }

            Console.WriteLine("[2/5] 正在构建目录树...");
            await sw.WriteLineAsync("## 1. 项目目录结构\n```text");
            await sw.WriteLineAsync($"{projectName}/");
            await _directoryTraversal.BuildTreeRecursiveAsync(rootPath, rootPath, "", sw, currentRegexes,
                excludedFolders, _fileSystem);
            await sw.WriteLineAsync("```\n---\n");

            Console.WriteLine("[3/5] 正在处理源码...");
            var seenPatterns = new HashSet<string>(StringComparer.Ordinal);
            var stats = await _directoryTraversal.ProcessDirectoryWithStatsAsync(
                rootPath, rootPath, sw, currentRegexes, taskMode,
                excludedClasses, preservedMethods, excludedFolders,
                reachableClasses, seenPatterns,
                _fileSystem, _codeAnalysis, _contentProcessor, _strategySelector);

            await sw.WriteLineAsync("\n<ImportantReminder>");
            await sw.WriteLineAsync("System Context Loaded. .NET 8 Strict Mode.");
            await sw.WriteLineAsync("</ImportantReminder>\n\n---");

            await sw.WriteLineAsync("## 3. 项目规模 with 统计");
            await sw.WriteLineAsync($"- **文件总数**: {stats.FileCount}");
            await sw.WriteLineAsync($"- **代码总行数**: {stats.LineCount}");
            await sw.WriteLineAsync($"- **安全状态**: 已自动执行正则脱敏");
            if (reachableClasses != null)
                await sw.WriteLineAsync(
                    $"- **引用过滤**: 已启用，入口类: {string.Join(", ", entryClasses)}，可达类数量: {reachableClasses.Count}");
        }

        Console.WriteLine("[5/5] 文档生成成功！");
        return outputPath;
    }

    private List<(Regex Regex, string Pattern)> PrepareIncludeRegexes(List<string> patterns)
    {
        var result = new List<(Regex Regex, string Pattern)>();
        if (patterns == null || patterns.Count == 0) return result;

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            string normalizedPattern = pattern.Replace('\\', '/');

            string regexPattern = Regex.Escape(normalizedPattern)
                .Replace(@"\*\*/", "(.+/)?")
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".");

            var re = new Regex("^" + regexPattern + "$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

            result.Add((re, regexPattern));
        }

        return result;
    }

    private async Task<string> GetProjectMetadataAsync(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath)) return string.Empty;

        var enumerationOptions = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 10 };

        var csprojFiles = Directory.EnumerateFiles(rootPath, "*.csproj", enumerationOptions)
            .Where(f => !ExcludedFolders.Any(ef =>
                f.Split(Path.DirectorySeparatorChar)
                    .Contains(ef, StringComparer.OrdinalIgnoreCase)));

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
                        true);
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
                sb.AppendLine($"  - **{Path.GetFileName(file)}** (解析失败: {ex.Message})");
            }
        }

        return found ? sb.ToString() : "- **依赖信息**: 未发现 .csproj 文件\n";
    }

    private static HashSet<string> ResolveReachableClasses(
        ClassGraphResult graphResult,
        IEnumerable<string> entryClasses,
        int maxDepth = -1)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);

        // 队列元素：(类名, 当前所在深度)
        var queue = new Queue<(string Name, int Depth)>();

        foreach (var entry in entryClasses)
        {
            if (!string.IsNullOrWhiteSpace(entry) && reachable.Add(entry))
                queue.Enqueue((entry, 0));
        }

        void DrainQueue()
        {
            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();

                // 已达到最大深度，不再继续展开此节点的引用
                if (maxDepth >= 0 && depth >= maxDepth)
                    continue;

                if (!graphResult.ReferenceGraph.TryGetValue(current, out var refs))
                    continue;

                foreach (var r in refs)
                {
                    if (reachable.Add(r))
                        queue.Enqueue((r, depth + 1));
                }
            }
        }

        DrainQueue();

        if (maxDepth != 0)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var (implClass, baseTypes) in graphResult.ImplementsMap)
                {
                    if (reachable.Contains(implClass)) continue;
                    if (!baseTypes.Any(b => reachable.Contains(b))) continue;

                    reachable.Add(implClass);
                    // 将实现类入队时，深度标记为 maxDepth（若受限），
                    // 确保其自身引用不会被进一步展开
                    int implDepth = maxDepth < 0 ? 0 : maxDepth;
                    queue.Enqueue((implClass, implDepth));
                    changed = true;
                }

                DrainQueue();
            } while (changed);
        }

        return reachable;
    }
}