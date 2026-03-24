using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FolderToDocument.Interfaces;

/// <summary>目录遍历服务接口</summary>
public interface IDirectoryTraversalService
{
    /// <summary>构建目录树</summary>
    Task BuildTreeRecursiveAsync(
        string currentPath,
        string rootPath,
        string indent,
        TextWriter tw,
        List<(Regex Regex, string Pattern)> includeRegexes,
        List<string> excludedFolders,
        IFileSystemService fileSystem);

    /// <summary>处理目录统计</summary>
    Task<ProjectStats> ProcessDirectoryWithStatsAsync(
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
        IOutputStrategySelector strategySelector);
}

/// <summary>项目统计信息</summary>
public record ProjectStats(int FileCount, long LineCount);