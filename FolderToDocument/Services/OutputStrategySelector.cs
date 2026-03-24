using FolderToDocument.Interfaces;

namespace FolderToDocument.Services;

/// <summary>输出策略选择实现</summary>
public class OutputStrategySelector : IOutputStrategySelector
{
    public FileOutputStrategy DetermineStrategy(
        string relPath,
        string fileName,
        string ext,
        string taskMode,
        HashSet<string> seenPatterns)
    {
        if (taskMode != "skeleton")
            return ext == ".cs" ? FileOutputStrategy.Skeleton : FileOutputStrategy.Full;

        if (ext == ".json")
            return FileOutputStrategy.JsonSchema;

        if (ext != ".cs")
            return FileOutputStrategy.Full;

        string normalized = relPath.Replace('\\', '/');

        if (normalized.Contains("/Interface/") || normalized.Contains("/IServices/"))
            return FileOutputStrategy.Full;

        if (normalized.Contains("/Enums/") || normalized.Contains("/Enum/"))
            return FileOutputStrategy.Full;

        if (normalized.Contains("/Models/") || normalized.Contains("/Data/") && !normalized.Contains("Module"))
            return FileOutputStrategy.Full;

        if (fileName.EndsWith("Module.cs", StringComparison.OrdinalIgnoreCase))
            return FileOutputStrategy.Full;

        if (fileName.Equals("GlobalUsings.cs", StringComparison.OrdinalIgnoreCase))
            return FileOutputStrategy.Full;

        if (normalized.Contains("/Commands/"))
        {
            string patternKey = "CommandHandler";
            if (seenPatterns.Add(patternKey + "_first"))
                return FileOutputStrategy.Skeleton;

            return FileOutputStrategy.UltraSkeleton;
        }

        if (normalized.Contains("/Impl/") && normalized.Contains("ConfigService"))
        {
            string patternKey = "ConfigServiceImpl";
            if (seenPatterns.Add(patternKey + "_first"))
                return FileOutputStrategy.Skeleton;
            return FileOutputStrategy.UltraSkeleton;
        }

        return FileOutputStrategy.Skeleton;
    }
}