using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using FolderToDocument.Interfaces;

namespace FolderToDocument.Services;

/// <summary>目录遍历服务实现</summary>
public class DirectoryTraversalService : IDirectoryTraversalService
{
    private static readonly FrozenSet<string> ExcludedFolders = new[]
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages", "Debug", "Release", ".idea", "dist", "build",
        "__pycache__", "Properties"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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

    public async Task BuildTreeRecursiveAsync(
        string currentPath,
        string rootPath,
        string indent,
        TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes,
        List<string> excludedFolders,
        IFileSystemService fileSystem)
    {
        var dirs = fileSystem.EnumerateDirectories(currentPath, "*", DefaultEnumOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .Where(d => excludedFolders == null ||
                        !excludedFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            .OrderBy(d => d).ToList();

        var files = fileSystem.EnumerateFiles(currentPath, "*", DefaultEnumOptions)
            .Where(f => !ExcludedExtensions.Contains(fileSystem.GetExtension(f).ToLower()))
            .OrderBy(f => f).ToList();

        var allItems = dirs.Concat(files)
            .Select(path => new { Path = path, IsDir = fileSystem.IsDirectory(path) })
            .Where(x => IsPathIncluded(fileSystem.GetRelativePath(x.Path, rootPath), includeRegexes, x.IsDir))
            .ToList();

        for (int i = 0; i < allItems.Count; i++)
        {
            try
            {
                bool isLast = (i == allItems.Count - 1);
                var item = allItems[i];
                string name = fileSystem.GetFileName(item.Path);

                string marker = !item.IsDir && (name.Equals("Program.cs") || name.Equals("App.xaml.cs") ||
                                                name.Equals("Startup.cs") || name.Equals("main.py"))
                    ? " [Entry Point]"
                    : "";

                await tw.WriteLineAsync($"{indent}{(isLast ? "└── " : "├── ")}{name}{(item.IsDir ? "/" : "")}{marker}");

                if (item.IsDir)
                {
                    await BuildTreeRecursiveAsync(item.Path, rootPath, indent + (isLast ? "    " : "│   "), tw,
                        includeRegexes, excludedFolders, fileSystem);
                }
            }
            catch (UnauthorizedAccessException)
            {
                await tw.WriteLineAsync($"{indent}└── [Access Denied]");
            }
        }
    }

    private bool IsPathIncluded(string relativePath, List<(Regex Regex, string Pattern)> includeRegexes, bool isDirectory)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath == ".") return true;
        if (includeRegexes == null || includeRegexes.Count == 0) return true;

        string normalized = relativePath.Replace('\\', '/').Trim('/');
        string fileName = Path.GetFileName(normalized);

        bool isMatch = includeRegexes.Any(r =>
            r.Regex.IsMatch(normalized) || (!string.IsNullOrEmpty(fileName) && r.Regex.IsMatch(fileName)));

        if (isMatch) return true;

        if (isDirectory)
        {
            string dirAsPrefix = normalized + "/";
            return includeRegexes.Any(r =>
                r.Pattern.StartsWith(dirAsPrefix, StringComparison.OrdinalIgnoreCase) ||
                r.Pattern.Contains("/" + dirAsPrefix, StringComparison.OrdinalIgnoreCase) ||
                (r.Pattern.StartsWith(".*") || r.Pattern.StartsWith("(.+/)?")));
        }

        return false;
    }

    public async Task<ProjectStats> ProcessDirectoryWithStatsAsync(
        string currentPath,
        string rootPath,
        TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes,
        string taskMode,
        List<string> excludedClasses,
        List<string> preservedMethods,
        List<string> excludedFolders,
        HashSet<string> reachableClasses,
        HashSet<string> seenPatterns,
        IFileSystemService fileSystem,
        ICodeAnalysisService codeAnalysis,
        IContentProcessor contentProcessor,
        IOutputStrategySelector strategySelector)
    {
        seenPatterns ??= new HashSet<string>(StringComparer.Ordinal);

        int fileCount = 0;
        long totalLines = 0;

        bool reachableFilterActive = reachableClasses != null;

        var files = fileSystem.EnumerateFiles(currentPath, "*", DefaultEnumOptions)
            .Where(f => !ExcludedExtensions.Contains(fileSystem.GetExtension(f).ToLowerInvariant()))
            .Where(f =>
            {
                string ext = fileSystem.GetExtension(f).ToLowerInvariant();
                string rel = fileSystem.GetRelativePath(f, rootPath);

                if (reachableFilterActive && ext == ".cs")
                    return true;

                return IsPathIncluded(rel, includeRegexes, false);
            })
            .OrderBy(f => f);

        foreach (var file in files)
        {
            string relPath = fileSystem.GetRelativePath(file, rootPath);
            string extension = fileSystem.GetExtension(file).ToLowerInvariant();
            string fileName = fileSystem.GetFileName(file);

            var strategy = strategySelector.DetermineStrategy(relPath, fileName, extension, taskMode, seenPatterns);

            if (strategy == FileOutputStrategy.Skip)
            {
                await tw.WriteLineAsync($"\n[FILE: {relPath}] // [已跳过：与前述文件模式相同]");
                fileCount++;
                continue;
            }

            await tw.WriteLineAsync($"\n[FILE: {relPath}]");

            try
            {
                if (strategy == FileOutputStrategy.JsonSchema)
                {
                    string rawJson = await File.ReadAllTextAsync(file, Encoding.UTF8);
                    string sanitized = contentProcessor.SanitizeSensitiveInfo(rawJson);
                    string schema = contentProcessor.ExtractJsonSchema(sanitized, maxArrayItems: 1);
                    string fence = schema.Contains("```") ? "~~~~" : "```";
                    await tw.WriteLineAsync($"{fence}json");
                    using var sr = new StringReader(schema);
                    totalLines += await WriteOptimizedContentAsync(sr, tw);
                    await tw.WriteLineAsync(fence);
                    fileCount++;
                    Console.WriteLine($"[Schema] {relPath}");
                    continue;
                }

                string preloadedSource = null;
                bool allClassesExcluded = false;

                if (extension == ".cs" && !IsConfigFile(file))
                {
                    bool needsClassFiltering = reachableClasses != null || excludedClasses is { Count: > 0 };
                    if (needsClassFiltering)
                    {
                        string rawSource = await File.ReadAllTextAsync(file, Encoding.UTF8);
                        preloadedSource = rawSource;

                        if (reachableClasses != null)
                        {
                            bool allRemoved;
                            (preloadedSource, allRemoved) =
                                await codeAnalysis.KeepOnlyReachableClassesAsync(preloadedSource, reachableClasses);
                            if (allRemoved)
                                allClassesExcluded = true;
                        }

                        if (!allClassesExcluded && excludedClasses is { Count: > 0 })
                        {
                            bool excludedAll;
                            (preloadedSource, excludedAll) =
                                await codeAnalysis.RemoveExcludedClassesAsync(preloadedSource, excludedClasses);
                            if (excludedAll)
                                allClassesExcluded = true;
                        }
                    }
                }

                if (allClassesExcluded)
                {
                    await tw.WriteLineAsync("// [已排除] 该文件中所有指定的类均已被过滤，不予展示。");
                    fileCount++;
                    Console.WriteLine($"[已排除] {relPath}");
                    continue;
                }

                if (strategy == FileOutputStrategy.UltraSkeleton && extension == ".cs" && !IsConfigFile(file))
                {
                    string sourceForUltra = preloadedSource ?? await File.ReadAllTextAsync(file, Encoding.UTF8);
                    string ultraSkeleton = await codeAnalysis.ExtractUltraSkeletonAsync(sourceForUltra);
                    string fence = ultraSkeleton.Contains("```") ? "~~~~" : "```";
                    await tw.WriteLineAsync($"{fence}csharp");
                    using var sr = new StringReader(ultraSkeleton);
                    totalLines += await WriteOptimizedContentAsync(sr, tw);
                    await tw.WriteLineAsync(fence);
                    fileCount++;
                    Console.WriteLine($"[UltraSkeleton] {relPath}");
                    continue;
                }

                if (!IsConfigFile(file) && taskMode == "skeleton" && extension == ".cs")
                {
                    string sourceForSkeleton = preloadedSource ?? await File.ReadAllTextAsync(file, Encoding.UTF8);
                    string skeleton = strategy == FileOutputStrategy.Full
                        ? sourceForSkeleton
                        : await codeAnalysis.ExtractCSharpSkeletonAsync(sourceForSkeleton, preservedMethods);

                    string fence = skeleton.Contains("```") ? "~~~~" : "```";
                    await tw.WriteLineAsync($"{fence}csharp");
                    using var sr = new StringReader(skeleton);
                    totalLines += await WriteOptimizedContentAsync(sr, tw);
                    await tw.WriteLineAsync(fence);
                }
                else if (!IsConfigFile(file) && taskMode == "explain")
                {
                    await tw.WriteLineAsync($"```{contentProcessor.GetFileExtensionForMarkdown(file)}");
                    if (preloadedSource != null)
                    {
                        using var sr = new StringReader(preloadedSource);
                        totalLines += await WriteOptimizedContentAsync(sr, tw);
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
                        totalLines += await WriteOptimizedContentAsync(streamReader, tw);
                    }

                    await tw.WriteLineAsync("```");
                }
                else
                {
                    string content = preloadedSource ?? await File.ReadAllTextAsync(file, Encoding.UTF8);
                    if (IsConfigFile(file)) content = contentProcessor.SanitizeSensitiveInfo(content);
                    if (taskMode != "explain") content = contentProcessor.StripComments(content, extension);

                    string fence = content.Contains("```") ? "~~~~" : "```";
                    await tw.WriteLineAsync($"{fence}{contentProcessor.GetFileExtensionForMarkdown(file)}");
                    using var sr = new StringReader(content);
                    totalLines += await WriteOptimizedContentAsync(sr, tw);
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

        var directories = fileSystem.EnumerateDirectories(currentPath, "*", DefaultEnumOptions)
            .Where(d => !ExcludedFolders.Contains(Path.GetFileName(d)))
            .Where(d => excludedFolders == null ||
                        !excludedFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            .OrderBy(d => d);

        foreach (var directory in directories)
        {
            string relDir = fileSystem.GetRelativePath(directory, rootPath);

            bool shouldRecurse = reachableFilterActive || IsPathIncluded(relDir, includeRegexes, true);

            if (shouldRecurse)
            {
                var subStats = await ProcessDirectoryWithStatsAsync(
                    directory, rootPath, tw, includeRegexes, taskMode,
                    excludedClasses, preservedMethods, excludedFolders,
                    reachableClasses, seenPatterns,
                    fileSystem, codeAnalysis, contentProcessor, strategySelector);
                fileCount += subStats.FileCount;
                totalLines += subStats.LineCount;
            }
        }

        return new ProjectStats(fileCount, totalLines);
    }

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
                if (previousLineWasEmpty) continue;
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

    private bool IsConfigFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        string fn = Path.GetFileName(filePath).ToLower();
        return ext == ".json" || ext == ".config" || ext == ".xml" || fn.Contains("setting") || fn.Contains("constant");
    }
}