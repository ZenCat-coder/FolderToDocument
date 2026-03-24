using System.Collections.Generic;

namespace FolderToDocument.Interfaces;

/// <summary>文件输出策略</summary>
public enum FileOutputStrategy
{
    /// <summary>完整内容</summary>
    Full,
    /// <summary>骨架代码</summary>
    Skeleton,
    /// <summary>极简骨架</summary>
    UltraSkeleton,
    /// <summary>JSON架构</summary>
    JsonSchema,
    /// <summary>跳过文件</summary>
    Skip
}

/// <summary>输出策略选择器</summary>
public interface IOutputStrategySelector
{
    /// <summary>确定输出策略</summary>
    FileOutputStrategy DetermineStrategy(
        string relPath,
        string fileName,
        string ext,
        string taskMode,
        HashSet<string> seenPatterns);
}