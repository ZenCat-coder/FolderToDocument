using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FolderToDocument.Interfaces;

/// <summary>类引用图结果</summary>
public record ClassGraphResult(
    IReadOnlyDictionary<string, HashSet<string>> ReferenceGraph,
    IReadOnlyDictionary<string, HashSet<string>> ImplementsMap
);

/// <summary>代码分析服务接口</summary>
public interface ICodeAnalysisService
{
    /// <summary>构建类引用图</summary>
    Task<ClassGraphResult> BuildClassReferenceGraphAsync(
        string rootPath,
        List<string> excludedFolders,
        IFileSystemService fileSystem);

    /// <summary>保留可达类</summary>
    Task<(string FilteredSource, bool AllRemoved)> KeepOnlyReachableClassesAsync(
        string source,
        HashSet<string> reachableClasses);

    /// <summary>移除排除类</summary>
    Task<(string FilteredSource, bool AllExcluded)> RemoveExcludedClassesAsync(
        string source,
        IReadOnlyCollection<string> excludedClasses);

    /// <summary>提取骨架代码</summary>
    Task<string> ExtractCSharpSkeletonAsync(
        string source,
        IReadOnlyCollection<string> preservedMethods = null);

    /// <summary>提取极简骨架</summary>
    Task<string> ExtractUltraSkeletonAsync(string source);
}