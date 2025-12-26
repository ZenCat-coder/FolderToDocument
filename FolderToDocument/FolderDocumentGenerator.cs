using System.Text;
using System.Text.RegularExpressions;

namespace FolderToDocument;

public class FolderDocumentGenerator
{
    private readonly List<string> _excludedExtensions = new List<string>
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".cache", ".user", ".suo",
        ".jpg", ".png", ".gif", ".ico", ".pdf", ".zip", ".rar", ".7z"
    };

    private readonly List<string> _excludedFolders = new List<string>
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages",
        "Debug", "Release", ".idea", "dist", "build", "__pycache__", "Properties"
    };

    private readonly List<(string pattern, string replacement)> _sensitivePatterns = new List<(string, string)>
    {
        (@"ConnectionString\s*=\s*[""']([^""']+)[""']", "ConnectionString=\"***\""),
        (@"\b(?:AppKey|Secret|Password|Token)\s*=\s*[""']([^""']+)[""']", "Property=\"***\""),
    };

    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"目录不存在: {rootPath}");

        // 1. 获取项目名
        string projectName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));

        // 2. 【智能路径逻辑】：解决嵌套文件夹问题
        if (string.IsNullOrEmpty(outputPath))
        {
            DirectoryInfo currentInfo = new DirectoryInfo(rootPath);
            DirectoryInfo parentInfo = currentInfo.Parent;

            // 如果当前目录名和父目录名一样（嵌套结构），再往上一层找真正的 Work/Review 空间
            if (parentInfo != null && parentInfo.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            {
                parentInfo = parentInfo.Parent;
            }

            string baseDir = parentInfo?.FullName ?? rootPath;
            outputPath = Path.Combine(baseDir, "Md", projectName, $"{projectName}.md");
        }

        Console.WriteLine($"[开始] 准备生成文档...");
        var sb = new StringBuilder();

        // 3. 头部信息
        sb.AppendLine($"# {projectName} 项目文档");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**项目路径**: {rootPath}");
        sb.AppendLine($"**说明**: 本文档包含项目目录树及代码内容");
        sb.AppendLine();

        // 4. 【目录树生成】
        Console.WriteLine("[目录树] 正在构建项目结构图...");
        sb.AppendLine("## 1. 项目目录结构");
        sb.AppendLine("```text");
        sb.AppendLine($"{projectName}/");
        BuildTreeRecursive(rootPath, "", sb);
        sb.AppendLine("```");
        sb.AppendLine("\n---\n");

        // 5. 【代码内容生成】
        sb.AppendLine("## 2. 详细代码内容");
        await ProcessDirectoryAsync(rootPath, rootPath, sb, 0);

        // 6. 保存
        string outputDir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);

        return outputPath;
    }

    private void BuildTreeRecursive(string currentPath, string indent, StringBuilder sb)
    {
        var directories = Directory.GetDirectories(currentPath)
            .Where(d => !_excludedFolders.Contains(Path.GetFileName(d)))
            .OrderBy(d => d).ToList();

        var files = Directory.GetFiles(currentPath)
            .Where(f => !_excludedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f).ToList();

        var allItems = directories.Cast<string>().Concat(files.Cast<string>()).ToList();

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
                BuildTreeRecursive(item, nextIndent, sb);
            }
        }
    }

    private async Task ProcessDirectoryAsync(string currentPath, string rootPath, StringBuilder sb, int level)
    {
        // 先处理当前文件夹下的文件
        var files = Directory.GetFiles(currentPath)
            .Where(file => !_excludedExtensions.Contains(Path.GetExtension(file).ToLower()))
            .OrderBy(file => file).ToList();

        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            string relPath = GetRelativePath(file, rootPath);
            
            // 在控制台显示进度
            Console.WriteLine($"[文件] 正在写入: {relPath}");

            sb.AppendLine($"### {fileName}");
            sb.AppendLine($"**文件路径**: `{relPath}`");
            sb.AppendLine();

            var content = await File.ReadAllTextAsync(file);
            if (IsConfigFile(file)) content = SanitizeSensitiveInfo(content);

            string lang = GetFileExtension(file);
            sb.AppendLine($"```{lang}");
            sb.AppendLine(content);
            sb.AppendLine("```\n");
        }

        // 再递归子目录
        var directories = Directory.GetDirectories(currentPath)
            .Where(dir => !_excludedFolders.Contains(Path.GetFileName(dir)))
            .OrderBy(dir => dir).ToList();

        foreach (var directory in directories)
        {
            await ProcessDirectoryAsync(directory, rootPath, sb, level + 1);
        }
    }

    private string GetRelativePath(string fullPath, string rootPath)
    {
        return Path.GetRelativePath(rootPath, fullPath);
    }

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