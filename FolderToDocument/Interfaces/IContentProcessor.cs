namespace FolderToDocument.Interfaces;

/// <summary>内容处理器接口</summary>
public interface IContentProcessor
{
    /// <summary>敏感信息脱敏</summary>
    string SanitizeSensitiveInfo(string content);

    /// <summary>去除代码注释</summary>
    string StripComments(string content, string extension);

    /// <summary>提取JSON架构</summary>
    string ExtractJsonSchema(string json, int maxArrayItems = 1);

    /// <summary>获取Markdown扩展名</summary>
    string GetFileExtensionForMarkdown(string filePath);
}