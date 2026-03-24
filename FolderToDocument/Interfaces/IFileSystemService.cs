using System.Collections.Generic;
using System.IO;

namespace FolderToDocument.Interfaces;

/// <summary>
/// 文件系统操作抽象
/// </summary>
public interface IFileSystemService
{
    /// <summary>枚举目录</summary>
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions options);

    /// <summary>枚举文件</summary>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions options);

    /// <summary>获取相对路径</summary>
    string GetRelativePath(string fullPath, string rootPath);

    /// <summary>目录是否存在</summary>
    bool DirectoryExists(string path);

    /// <summary>是否为目录</summary>
    bool IsDirectory(string path);

    /// <summary>获取文件名</summary>
    string GetFileName(string path);

    /// <summary>获取扩展名</summary>
    string GetExtension(string path);
}