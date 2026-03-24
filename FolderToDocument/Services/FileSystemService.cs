using System.Collections.Generic;
using System.IO;
using FolderToDocument.Interfaces;

namespace FolderToDocument.Services;

/// <summary>
/// 文件系统服务实现
/// </summary>
public class FileSystemService : IFileSystemService
{
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions options)
        => Directory.EnumerateDirectories(path, searchPattern, options);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions options)
        => Directory.EnumerateFiles(path, searchPattern, options);

    public string GetRelativePath(string fullPath, string rootPath)
        => Path.GetRelativePath(rootPath, fullPath);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool IsDirectory(string path) => Directory.Exists(path);

    public string GetFileName(string path) => Path.GetFileName(path);

    public string GetExtension(string path) => Path.GetExtension(path);
}