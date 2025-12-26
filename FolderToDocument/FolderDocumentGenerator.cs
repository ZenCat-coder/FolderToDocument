using System.Text;
using System.Text.RegularExpressions;

namespace FolderToDocument;

public class FolderDocumentGenerator
{
    private readonly List<string> _excludedExtensions = new List<string> { ".exe", ".dll", ".pdb", ".bin", ".obj", ".cache", ".user", ".suo", ".jpg", ".png", ".gif", ".ico", ".pdf", ".zip", ".rar", ".7z" };
    private readonly List<string> _excludedFolders = new List<string> { "bin", "obj", ".vs", ".git", "node_modules", "packages", "Debug", "Release", ".idea", "dist", "build", "__pycache__", "Properties" };

    private readonly List<(string pattern, string replacement)> _sensitivePatterns = new List<(string, string)>
    {
        (@"ConnectionString\s*=\s*[""']([^""']+)[""']", "ConnectionString=\"***\""),
        (@"\b(?:AppKey|Secret|Password|Token)\s*=\s*[""']([^""']+)[""']", "Property=\"***\""),
    };

    // 存储转换后的正则表达式
    private List<Regex> _includeRegexes = new();

    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null, List<string> includedPatterns = null)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"目录不存在: {rootPath}");

        // 初始化包含规则
        PrepareIncludeRegexes(includedPatterns);

        string projectName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));

        if (string.IsNullOrEmpty(outputPath))
        {
            DirectoryInfo currentInfo = new DirectoryInfo(rootPath);
            DirectoryInfo parentInfo = currentInfo.Parent;
            if (parentInfo != null && parentInfo.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)) parentInfo = parentInfo.Parent;
            string baseDir = parentInfo?.FullName ?? rootPath;
            outputPath = Path.Combine(baseDir, "Md", projectName, $"{projectName}.md");
        }

        Console.WriteLine($"[开始] 准备生成文档 (包含模式: {(includedPatterns?.Count > 0 ? "已启用" : "全部包含")})");
        var sb = new StringBuilder();

        // 头部信息
        sb.AppendLine($"# {projectName} 项目文档");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**项目路径**: {rootPath}");
        if (includedPatterns?.Count > 0) sb.AppendLine($"**筛选模式**: {string.Join(", ", includedPatterns)}");
        sb.AppendLine();

        // 1. 目录树
        sb.AppendLine("## 1. 项目目录结构");
        sb.AppendLine("```text");
        sb.AppendLine($"{projectName}/");
        BuildTreeRecursive(rootPath, rootPath, "", sb);
        sb.AppendLine("```");
        sb.AppendLine("\n---\n");

        // 2. 代码内容
        sb.AppendLine("## 2. 详细代码内容");
        await ProcessDirectoryAsync(rootPath, rootPath, sb, 0);

        string outputDir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);

        return outputPath;
    }

    // 将 glob 模式 (如 **/*.cs) 转换为 Regex
    private void PrepareIncludeRegexes(List<string> patterns)
    {
        _includeRegexes = new List<Regex>();
        if (patterns == null || patterns.Count == 0) return;

        foreach (var pattern in patterns)
        {
            // 将通配符转换为正则表达式
            string regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*/", "(.+/)?")  // ** 匹配任意目录
                .Replace(@"\*\*", ".*")        // ** 匹配任意字符
                .Replace(@"\*", "[^/]*")       // * 匹配单层目录下的文件
                .Replace(@"\?", ".") + "$";
            
            _includeRegexes.Add(new Regex(regexPattern, RegexOptions.IgnoreCase));
        }
    }

    // 检查路径是否符合包含规则
    private bool IsPathIncluded(string relativePath)
    {
        if (_includeRegexes.Count == 0) return true; // 如果没设规则，默认全包含
        
        // 统一路径分隔符为正斜杠，方便正则匹配
        string normalizedPath = relativePath.Replace("\\", "/");
        
        // 如果是文件夹路径，检查规则是否涵盖了它的子项
        return _includeRegexes.Any(re => re.IsMatch(normalizedPath) || 
                                         normalizedPath.Split('/').Any(part => re.IsMatch(part)) ||
                                         re.ToString().Contains(normalizedPath));
    }

    private void BuildTreeRecursive(string currentPath, string rootPath, string indent, StringBuilder sb)
    {
        var directories = Directory.GetDirectories(currentPath)
            .Where(d => !_excludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        var files = Directory.GetFiles(currentPath)
            .Where(f => !_excludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f).ToList();

        // 过滤：只保留符合规则的项
        var allItems = directories.Cast<string>().Concat(files.Cast<string>())
            .Where(path => IsPathIncluded(GetRelativePath(path, rootPath)))
            .ToList();

        for (int i = 0; i < allItems.Count; i++)
        {
            bool isLast = (i == allItems.Count - 1);
            var item = allItems[i];
            string itemName = Path.GetFileName(item);
            bool isDirectory = Directory.Exists(item);

            sb.Append(indent);
            sb.Append(isLast ? "└── " : "├── ");
            sb.Append(itemName);
            if (isDirectory) sb.Append("/");
            sb.AppendLine();

            if (isDirectory)
            {
                string nextIndent = indent + (isLast ? "    " : "│   ");
                BuildTreeRecursive(item, rootPath, nextIndent, sb);
            }
        }
    }

    private async Task ProcessDirectoryAsync(string currentPath, string rootPath, StringBuilder sb, int level)
    {
        var files = Directory.GetFiles(currentPath)
            .Where(f => !_excludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .Where(f => IsPathIncluded(GetRelativePath(f, rootPath))) // 过滤内容
            .OrderBy(f => f).ToList();

        foreach (var file in files)
        {
            string relPath = GetRelativePath(file, rootPath);
            Console.WriteLine($"[写入] {relPath}");
            sb.AppendLine($"### {Path.GetFileName(file)}");
            sb.AppendLine($"**文件路径**: `{relPath}`");
            sb.AppendLine();

            var content = await File.ReadAllTextAsync(file);
            if (IsConfigFile(file)) content = SanitizeSensitiveInfo(content);

            string lang = GetFileExtension(file);
            sb.AppendLine($"```{lang}");
            sb.AppendLine(content);
            sb.AppendLine("```\n");
        }

        var directories = Directory.GetDirectories(currentPath)
            .Where(d => !_excludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        foreach (var directory in directories)
        {
            await ProcessDirectoryAsync(directory, rootPath, sb, level + 1);
        }
    }

    private string GetRelativePath(string fullPath, string rootPath) => Path.GetRelativePath(rootPath, fullPath);

    private bool IsConfigFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return ext == ".json" || ext == ".config" || ext == ".xml";
    }

    private string SanitizeSensitiveInfo(string content)
    {
        foreach (var (pattern, replacement) in _sensitivePatterns)
            content = Regex.Replace(content, pattern, replacement, RegexOptions.IgnoreCase);
        return content;
    }

    private string GetFileExtension(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return ext switch { ".cs" => "csharp", ".json" => "json", ".xml" => "xml", ".csproj" => "xml", _ => "text" };
    }
}