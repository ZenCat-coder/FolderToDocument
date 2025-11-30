using System.Text;
using System.Text.RegularExpressions;

namespace FolderToDocument;

/// <summary>
/// 文件夹文档生成器
/// 用于将项目文件夹结构及代码文件内容转换为结构化文档
/// </summary>
public class FolderDocumentGenerator
{
    //包含的文件模式列表（支持通配符）
    private readonly List<string> _includedPatterns = new List<string>();

    // 排除的文件扩展名列表（这些文件通常不需要包含在文档中）
    private readonly List<string> _excludedExtensions = new List<string>
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".cache", ".user", ".suo",
        ".jpg", ".png", ".gif", ".ico", ".pdf", ".zip", ".rar", ".7z"
    };

    // 排除的文件夹列表（这些文件夹通常是构建输出或依赖项）
    private readonly List<string> _excludedFolders = new List<string>
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages",
        "Debug", "Release", ".idea", "dist", "build", "__pycache__"
    };

    // 敏感信息模式 - 用于脱敏处理
    // 敏感信息模式 - 增强版的脱敏处理
    private readonly List<(string pattern, string replacement)> _sensitivePatterns = new List<(string, string)>
    {
        // 连接字符串
        (@"ConnectionString\s*=\s*[""']([^""']+)[""']", "ConnectionString=\"***\""),
        (@"ConnectionStrings[^}]+}", "ConnectionStrings: { *** }"),

        // Data Source 等数据库连接信息
        (@"Data\s+Source\s*=\s*[^;]+;", "Data Source=***;"),
        (@"Server\s*=\s*[^;]+;", "Server=***;"),
        (@"Initial\s+Catalog\s*=\s*[^;]+;", "Initial Catalog=***;"),

        // 增强的密钥匹配 - 使用更全面的模式
        (@"\b(?:App|Api|Access|Auth|User|Client|Private|Public|Secret)?Key\s*=\s*[""']([^""']+)[""']", "Key=\"***\""),
        (@"\b(?:App|Api|Access|Auth|User|Client|Private|Public)?Secret\s*=\s*[""']([^""']+)[""']", "Secret=\"***\""),

        // 增强的 Token 匹配 - 包含所有常见变体
        (@"\b(?:Access|Refresh|Bearer|Auth|User|Client)?Token\s*=\s*[""']([^""']+)[""']", "Token=\"***\""),

        // 密码
        (@"\b(?:Password|Pwd)\s*=\s*[""']([^""']+)[""']", "Password=\"***\""),

        // 数据库凭据
        (@"User\s*ID\s*=\s*[^;]+;", "User ID=***;"),
        (@"User\s*Id\s*=\s*[^;]+;", "User Id=***;"),
        (@"User\s*Name\s*=\s*[^;]+;", "User Name=***;"),
        (@"UID\s*=\s*[^;]+;", "UID=***;"),
        (@"Password\s*=\s*[^;]+;", "Password=***;"),
        (@"Pwd\s*=\s*[^;]+;", "Pwd=***;"),

        // 各种密钥ID
        (@"\b(?:AKLT|TVR)[a-zA-Z0-9]+\b", "***ID***"),
        (@"\b[A-Za-z0-9]{20,}\b", "***LONG_ID***"), // 匹配长ID

        // URL 中的敏感信息
        (@"https?://[^\s/]+?(:[^\s/]+)?@[^\s]+", "http://***:***@***"), // 带认证的URL
        (@"\?[^&\s]*(?:key|token|secret|password|pwd)=[^&\s]+", "?***=***"), // URL参数

        // 手机号脱敏
        (@"1[3-9]\d{9}", "138****0000"),
        (@"\+?86\s*1[3-9]\d{9}", "+86 138****0000"),

        // 邮箱脱敏
        (@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", "user@example.com"),

        // JSON 格式配置 - 增强版
        (@"(""(?:AppKey|ApiKey|AccessKey|SecretKey|ClientKey|PrivateKey|PublicKey|AuthKey|UserKey|AppSecret|ApiSecret|ClientSecret|PrivateSecret|PublicSecret|AuthSecret|AccessToken|RefreshToken|BearerToken|AuthToken|UserToken|ClientToken|SmsKey|ModelId|AccessKeyId|SecretAccessKey|SmsAccount|TemplateID|Password|Pwd)""\s*:\s*)""([^""']*)""",
            "$1\"***\""),

        // 等号格式 - 增强版
        (@"\b(?:AppKey|ApiKey|AccessKey|SecretKey|ClientKey|PrivateKey|PublicKey|AuthKey|UserKey|AppSecret|ApiSecret|ClientSecret|PrivateSecret|PublicSecret|AuthSecret|AccessToken|RefreshToken|BearerToken|AuthToken|UserToken|ClientToken|SmsKey|ModelId|AccessKeyId|SecretAccessKey|SmsAccount|TemplateID|Password|Pwd)\s*=\s*([^\s,;]+)",
            "$1=***"),

        // 通用模式：匹配任何看起来像密钥的长字符串
        (@"\b[a-zA-Z0-9+/=]{32,}\b", "***LONG_SECRET***"), // 32位以上的base64字符串
        (@"\b[0-9a-f]{32,}\b", "***HEX_SECRET***"), // 32位以上的十六进制字符串

        // 新增：专门针对JSON格式的敏感信息处理
        (@"(""(?:AppKey|AppSecret|Session|ApiKey|SecretKey|Token|Password|Pwd|ConnectionString|PgSqlConnectionString)""\s*:\s*)""([^""']+)""",
            "$1\"***\""),

        // 新增：针对嵌套JSON对象的敏感信息
        (@"(""(?:TaobaoApi|DbStrConfig|ConnectionStrings|AppSettings)""\s*:\s*\{[^}]*(?:(?:AppKey|AppSecret|Session|ConnectionString)[^}]*)*\})",
            "$1: { *** }"),

        // 新增：针对数据库连接字符串的完整匹配
        (@"(""(?:PgSqlConnectionString|ConnectionString|ConnString|ConnStr)""\s*:\s*)""([^""']+)""",
            "$1\"***\""),

        // 增强：更全面的JSON键值对匹配
        (@"(""(?i)(?:key|secret|token|password|pwd|session|connection)(?-i)[^""]*""\s*:\s*)""([^""']+)""",
            "$1\"***\""),
    };

    /// <summary>
    /// 生成项目文档
    /// </summary>
    /// <param name="rootPath">要扫描的根目录路径</param>
    /// <param name="outputPath">输出文件路径（可选）</param>
    /// <param name="includedPatterns">包含的文件模式列表（可选）</param>
    /// <returns>生成的文档完整路径</returns>
    public async Task<string> GenerateDocumentAsync(string rootPath, string outputPath = null,
        List<string> includedPatterns = null)
    {
        // 参数验证
        if (string.IsNullOrEmpty(rootPath))
            throw new ArgumentException("根目录路径不能为空", nameof(rootPath));

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"目录不存在: {rootPath}");

        // 设置包含模式
        if (includedPatterns != null && includedPatterns.Any())
        {
            SetIncludedPatterns(includedPatterns);
            Console.WriteLine($"[过滤] 包含模式: {string.Join(", ", includedPatterns)}");
        }

        // 如果未指定输出路径，使用默认路径
        if (string.IsNullOrEmpty(outputPath))
        {
            // 获取根目录的最后一个目录名
            string rootDirectoryName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(rootDirectoryName))
            {
                rootDirectoryName = "项目文档";
            }

            // 构建默认输出路径：项目根目录的父目录 + Md + 项目名 + 项目名.md
            string parentDir = Directory.GetParent(rootPath)?.FullName;
            if (string.IsNullOrEmpty(parentDir))
            {
                // 如果没有父目录，回退到项目根目录
                outputPath = Path.Combine(rootPath, $"{rootDirectoryName}.md");
            }
            else
            {
                // 构建路径：父目录\Md\项目名\项目名.md
                outputPath = Path.Combine(parentDir, "Md", rootDirectoryName, $"{rootDirectoryName}.md");
            }
        }

        Console.WriteLine("[开始] 开始生成文档...");
        Console.WriteLine($"[扫描] 扫描目录: {rootPath}");
        Console.WriteLine($"[输出] 输出文件: {outputPath}");

        var sb = new StringBuilder();

        // 添加文档标题和元信息
        string projectName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));
        sb.AppendLine($"# {projectName} 项目文档");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**项目路径**: {rootPath}");
        sb.AppendLine($"**文档路径**: {outputPath}");
        sb.AppendLine($"**说明**: 本文档已自动脱敏处理，隐藏了敏感配置信息");
        sb.AppendLine();

        // 递归处理文件夹结构
        await ProcessDirectoryAsync(rootPath, rootPath, sb, 0);

        string result = sb.ToString();

        try
        {
            // 确保输出目录存在
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"[目录] 创建输出目录: {outputDir}");
            }

            // 写入文件到指定路径
            await File.WriteAllTextAsync(outputPath, result, Encoding.UTF8);

            Console.WriteLine($"[成功] 文档生成成功！");
            Console.WriteLine($"[文件] 文件位置: {outputPath}");
            Console.WriteLine($"[大小] 文件大小: {new FileInfo(outputPath).Length} 字节");

            return outputPath;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[错误] 权限不足，无法写入文件: {outputPath}");
            Console.WriteLine($"[详情] 错误详情: {ex.Message}");

            // 尝试回退到当前目录
            string fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), $"{projectName}.md");
            Console.WriteLine($"[回退] 尝试回退到当前目录: {fallbackPath}");

            await File.WriteAllTextAsync(fallbackPath, result, Encoding.UTF8);
            Console.WriteLine($"[成功] 文档已保存到回退位置: {fallbackPath}");

            return fallbackPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 保存文件时出错: {ex.Message}");
            throw;
        }
    }


    /// <summary>
    /// 递归处理目录及其内容
    /// </summary>
    /// <param name="currentPath">当前处理的目录路径</param>
    /// <param name="rootPath">根目录路径（用于计算相对路径）</param>
    /// <param name="sb">StringBuilder用于构建文档内容</param>
    /// <param name="level">当前目录层级（用于缩进）</param>
    private async Task ProcessDirectoryAsync(string currentPath, string rootPath, StringBuilder sb, int level)
    {
        try
        {
            // 检查目录是否应该被包含
            if (!ShouldIncludeDirectory(currentPath, rootPath))
            {
                Console.WriteLine($"[跳过] 目录不在包含模式中: {GetRelativePath(currentPath, rootPath)}");
                return;
            }

            // 获取当前目录下的所有子目录（排除不需要的文件夹）
            var directories = Directory.GetDirectories(currentPath)
                .Where(dir => !_excludedFolders.Contains(Path.GetFileName(dir)))
                .Where(dir => ShouldIncludeDirectory(dir, rootPath)) // 新增过滤
                .OrderBy(dir => dir)
                .ToList();

            // 获取当前目录下的所有文件（排除不需要的文件类型）
            var files = Directory.GetFiles(currentPath)
                .Where(file => !_excludedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .Where(file => ShouldIncludeFile(file, rootPath)) // 新增过滤
                .OrderBy(file => file)
                .ToList();

            // 如果没有文件且没有子目录，则跳过这个空目录
            if (!files.Any() && !directories.Any())
                return;

            // 处理当前目录的文件
            foreach (var file in files)
            {
                await ProcessFileAsync(file, rootPath, sb, level);
            }

            // 递归处理子目录
            foreach (var directory in directories)
            {
                var dirName = Path.GetFileName(directory);

                // 添加目录标题
                var headingLevel = Math.Min(level + 2, 6);
                var headingMark = new string('#', headingLevel);
                sb.AppendLine($"{headingMark} {dirName}");
                sb.AppendLine();

                await ProcessDirectoryAsync(directory, rootPath, sb, level + 1);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 无权限访问的目录，记录日志但继续处理其他目录
            var relativePath = GetRelativePath(currentPath, rootPath);
            Console.WriteLine($"[警告] 跳过无访问权限的目录: {relativePath}");

            var headingLevel = Math.Min(level + 2, 6);
            var headingMark = new string('#', headingLevel);
            sb.AppendLine($"{headingMark} {Path.GetFileName(currentPath)} [无访问权限]");
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            // 其他异常，记录日志但继续处理
            Console.WriteLine($"[警告] 处理目录 {currentPath} 时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理单个文件，读取内容并添加到文档
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="rootPath">根目录路径</param>
    /// <param name="sb">StringBuilder用于构建文档内容</param>
    /// <param name="level">当前文件所在层级</param>
    private async Task ProcessFileAsync(string filePath, string rootPath, StringBuilder sb, int level)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var relativePath = GetRelativePath(filePath, rootPath);

            // 添加文件标题
            var headingLevel = Math.Min(level + 2, 6);
            var headingMark = new string('#', headingLevel);
            sb.AppendLine($"{headingMark} {fileName}");
            sb.AppendLine();

            // 添加文件路径信息
            sb.AppendLine($"**文件路径**: `{relativePath}`");
            sb.AppendLine();

            // 读取文件内容
            var content = await File.ReadAllTextAsync(filePath);

            // 对配置文件进行脱敏处理
            if (IsConfigFile(filePath))
            {
                content = SanitizeSensitiveInfo(content);
                sb.AppendLine("> [注意] 此配置文件已自动脱敏处理，敏感信息已被替换");
                sb.AppendLine();
            }

            // 添加文件内容（使用Markdown代码块格式）
            string codeLanguage = GetFileExtension(filePath);
            sb.AppendLine($"```{codeLanguage}");
            sb.AppendLine(content);
            sb.AppendLine("```");
            sb.AppendLine(); // 空行分隔

            Console.WriteLine($"[文件] 已处理文件: {relativePath}");
        }
        catch (UnauthorizedAccessException)
        {
            // 无权限读取的文件
            var relativePath = GetRelativePath(filePath, rootPath);
            Console.WriteLine($"[警告] 跳过无访问权限的文件: {relativePath}");

            var headingLevel = Math.Min(level + 2, 6);
            var headingMark = new string('#', headingLevel);
            sb.AppendLine($"{headingMark} {Path.GetFileName(filePath)} [无访问权限]");
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            // 其他文件读取异常
            Console.WriteLine($"[警告] 处理文件 {filePath} 时出错: {ex.Message}");

            var headingLevel = Math.Min(level + 2, 6);
            var headingMark = new string('#', headingLevel);
            sb.AppendLine($"{headingMark} {Path.GetFileName(filePath)} [读取失败: {ex.Message}]");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// 检查是否为配置文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否为配置文件</returns>
    private bool IsConfigFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLower();
        var extension = Path.GetExtension(filePath).ToLower();

        // 扩展配置文件识别范围
        return fileName.Contains("appsettings") ||
               fileName.Contains("config") ||
               fileName.Contains("setting") ||
               fileName.Contains("app.config") ||
               fileName.Contains("web.config") ||
               extension == ".config" ||
               extension == ".json" || // 所有json文件都视为配置
               extension == ".yml" ||
               extension == ".yaml" ||
               (extension == ".xml" && fileName.Contains("config")) ||
               fileName.EndsWith(".env") ||
               fileName == ".env";
    }

    /// <summary>
    /// 脱敏处理敏感信息 - 增强版
    /// </summary>
    /// <param name="content">原始内容</param>
    /// <returns>脱敏后的内容</returns>
    private string SanitizeSensitiveInfo(string content)
    {
        string sanitizedContent = content;

        Console.WriteLine("[脱敏] 开始脱敏处理...");

        // 首先处理JSON格式的配置
        if (IsJsonFile("[dummy]")) // 检查是否为JSON格式
        {
            Console.WriteLine("[脱敏] 检测到JSON格式配置");

            // 专门处理JSON格式的敏感信息
            sanitizedContent = SanitizeJsonContent(sanitizedContent);
        }

        // 应用所有正则表达式模式
        foreach (var (pattern, replacement) in _sensitivePatterns)
        {
            try
            {
                int beforeCount = Regex.Matches(sanitizedContent, pattern, RegexOptions.IgnoreCase).Count;
                sanitizedContent = Regex.Replace(sanitizedContent, pattern, replacement, RegexOptions.IgnoreCase);
                int afterCount = Regex.Matches(sanitizedContent, pattern, RegexOptions.IgnoreCase).Count;

                if (beforeCount > afterCount)
                {
                    Console.WriteLine($"[脱敏] 应用模式: {GetPatternDescription(pattern)}");
                    Console.WriteLine($"[脱敏] 替换次数: {beforeCount - afterCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[警告] 脱敏处理出错，模式: {GetPatternDescription(pattern)}, 错误: {ex.Message}");
            }
        }

        // 最终检查确保敏感信息被替换
        sanitizedContent = EnsureSensitiveInfoReplaced(sanitizedContent);

        Console.WriteLine("[脱敏] 脱敏处理完成");
        return sanitizedContent;
    }

    // 新增：专门处理JSON内容的脱敏
    private string SanitizeJsonContent(string content)
    {
        string jsonContent = content;

        // 处理 TaobaoApi 配置块
        jsonContent = Regex.Replace(jsonContent,
            @"""TaobaoApi""\s*:\s*\{[^\}]*""AppKey""\s*:\s*""[^""]*"",\s*""AppSecret""\s*:\s*""[^""]*"",\s*""Session""\s*:\s*""[^""]*""[^\}]*\}",
            @"""TaobaoApi"": {
    ""AppKey"": ""***"",
    ""AppSecret"": ""***"",
    ""Session"": ""***"",
    ""GatewayUrl"": ""https://eco.taobao.com/router/rest""
  }");

        // 处理 DbStrConfig 配置块
        jsonContent = Regex.Replace(jsonContent,
            @"""DbStrConfig""\s*:\s*\{[^\}]*""PgSqlConnectionString""\s*:\s*""[^""]*""[^\}]*\}",
            @"""DbStrConfig"": {
    ""PgSqlConnectionString"": ""***""
  }");

        return jsonContent;
    }

// 新增：检查是否为JSON文件
    private bool IsJsonFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension == ".json" ||
               Path.GetFileName(filePath).ToLower().Contains("appsettings") ||
               Path.GetFileName(filePath).ToLower().Contains("config.json");
    }

// 新增：获取模式描述（用于日志）
    private string GetPatternDescription(string pattern)
    {
        if (pattern.Length > 60)
        {
            return pattern.Substring(0, 57) + "...";
        }

        return pattern;
    }


    /// <summary>
    /// 确保敏感信息被替换 - 最终检查
    /// </summary>
    private string EnsureSensitiveInfoReplaced(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            string processedLine = line;

            // 检查是否需要脱敏
            if (ContainsSensitivePattern(processedLine) && !IsAlreadySanitized(processedLine))
            {
                // 对JSON格式进行强制替换
                processedLine = Regex.Replace(processedLine,
                    @"""(\w*(?:Key|Secret|Token|Password|Pwd|Session|ConnectionString)\w*)""\s*:\s*""([^""']*)""",
                    "\"$1\":\"***\"");

                // 对等号格式进行强制替换
                processedLine = Regex.Replace(processedLine,
                    @"(\w*(?:Key|Secret|Token|Password|Pwd|Session|ConnectionString)\w*)\s*=\s*([^\s,;]+)",
                    "$1=***");

                Console.WriteLine($"[脱敏] 强制替换敏感行: {line.Trim()}");
            }

            result.Add(processedLine);
        }

        return string.Join("\n", result);
    }

    /// <summary>
    /// 检查是否包含敏感模式
    /// </summary>
    private bool ContainsSensitivePattern(string line)
    {
        var sensitiveKeywords = new[]
        {
            "Key", "Secret", "Token", "Password", "Pwd", "ConnectionString",
            "Session", "AppKey", "AppSecret", "PgSqlConnectionString"
        };
        return sensitiveKeywords.Any(keyword =>
            line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// 检查是否已经被脱敏处理
    /// </summary>
    private bool IsAlreadySanitized(string line)
    {
        return line.Contains("***") || line.Contains("****");
    }

    /// <summary>
    /// 获取相对于根目录的路径（修复版本）
    /// </summary>
    /// <param name="fullPath">完整路径</param>
    /// <param name="rootPath">根目录路径</param>
    /// <returns>相对路径</returns>
    private string GetRelativePath(string fullPath, string rootPath)
    {
        try
        {
            // 确保路径格式一致
            rootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            fullPath = Path.GetFullPath(fullPath);

            // 使用 Path.GetRelativePath（.NET Core 2.0+ 支持）
            var relativePath = Path.GetRelativePath(rootPath, fullPath);

            // 如果返回的是当前目录符号，转换为空字符串
            if (relativePath == ".")
                return string.Empty;

            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[警告] 计算相对路径失败: {fullPath} -> {rootPath}, 错误: {ex.Message}");
            return fullPath; // 如果计算相对路径失败，返回完整路径
        }
    }

    /// <summary>
    /// 根据文件扩展名获取对应的代码语言标识（用于Markdown代码块）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>代码语言标识</returns>
    private string GetFileExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".html" => "html",
            ".css" => "css",
            ".json" => "json",
            ".xml" => "xml",
            ".md" => "markdown",
            ".sql" => "sql",
            ".py" => "python",
            ".java" => "java",
            ".cpp" => "cpp",
            ".c" => "c",
            ".php" => "php",
            ".rb" => "ruby",
            ".go" => "go",
            ".rs" => "rust",
            ".swift" => "swift",
            ".kt" => "kotlin",
            ".config" => "xml",
            ".csproj" => "xml",
            ".sln" => "text",
            ".txt" => "text",
            _ => "text" // 默认使用text
        };
    }

    #region 新方法

    /// <summary>
    /// 设置包含的文件模式
    /// </summary>
    /// <param name="patterns">文件模式列表，支持通配符，如："*.cs", "ZYCsjOrderReceiptBusinessModule/**"</param>
    public void SetIncludedPatterns(List<string> patterns)
    {
        _includedPatterns.Clear();
        if (patterns != null && patterns.Any())
        {
            _includedPatterns.AddRange(patterns);
        }
    }

    /// <summary>
    /// 检查目录是否应该被包含（简化版本）
    /// </summary>
    private bool ShouldIncludeDirectory(string dirPath, string rootPath)
    {
        // 如果没有设置包含模式，包含所有目录
        if (!_includedPatterns.Any())
            return true;

        var relativePath = GetRelativePath(dirPath, rootPath);

        Console.WriteLine($"[调试] 检查目录: {relativePath}");

        // 根目录总是包含
        if (string.IsNullOrEmpty(relativePath) || relativePath == ".")
            return true;

        foreach (var pattern in _includedPatterns)
        {
            // 处理目录通配符模式
            if (pattern.EndsWith("/**"))
            {
                var directoryPattern = pattern.Substring(0, pattern.Length - 3);
                if (relativePath.StartsWith(directoryPattern + "/") ||
                    relativePath.Equals(directoryPattern, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[包含] 目录通配符匹配: {relativePath} 匹配 {pattern}");
                    return true;
                }
            }
            // 检查目录是否在模式指定的路径中
            else if (pattern.StartsWith(relativePath + "/"))
            {
                Console.WriteLine($"[包含] 目录包含匹配文件: {relativePath} 匹配 {pattern}");
                return true;
            }
            // 检查模式是否在当前目录中
            else if (relativePath.StartsWith(Path.GetDirectoryName(pattern)?.Replace('\\', '/') ?? ""))
            {
                Console.WriteLine($"[包含] 目录可能包含匹配文件: {relativePath} 匹配 {pattern}");
                return true;
            }
        }

        // 如果目录本身不匹配，检查它是否包含任何匹配的文件
        try
        {
            var files = Directory.GetFiles(dirPath)
                .Where(file => !_excludedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            foreach (var file in files)
            {
                if (ShouldIncludeFile(file, rootPath))
                {
                    Console.WriteLine($"[包含] 目录包含匹配文件: {relativePath}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[警告] 检查目录文件失败: {dirPath}, 错误: {ex.Message}");
        }

        Console.WriteLine($"[跳过] 目录不匹配任何模式: {relativePath}");
        return false;
    }

    /// <summary>
    /// 通配符匹配（修复版本）
    /// </summary>
    private bool MatchesWildcard(string input, string pattern)
    {
        try
        {
            string regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            // 如果正则表达式解析失败，回退到简单匹配
            return input.Contains(pattern.Replace("*", "").Replace("?", ""));
        }
    }

    /// <summary>
    /// 检查文件是否应该被包含（简化版本）
    /// </summary>
    private bool ShouldIncludeFile(string filePath, string rootPath)
    {
        // 如果没有设置包含模式，包含所有文件
        if (!_includedPatterns.Any())
            return true;

        var relativePath = GetRelativePath(filePath, rootPath);
        var fileName = Path.GetFileName(filePath);

        Console.WriteLine($"[调试] 检查文件: {relativePath}");

        foreach (var pattern in _includedPatterns)
        {
            // 处理目录通配符模式
            if (pattern.EndsWith("/**"))
            {
                var directoryPattern = pattern.Substring(0, pattern.Length - 3);
                if (relativePath.StartsWith(directoryPattern + "/"))
                {
                    Console.WriteLine($"[包含] 目录匹配: {relativePath} 匹配 {pattern}");
                    return true;
                }
            }
            // 处理通配符文件名模式
            else if (pattern.Contains("*") || pattern.Contains("?"))
            {
                // 检查文件名匹配
                if (MatchesWildcard(fileName, pattern))
                {
                    Console.WriteLine($"[包含] 文件名通配符匹配: {fileName} 匹配 {pattern}");
                    return true;
                }

                // 检查相对路径匹配
                if (MatchesWildcard(relativePath, pattern))
                {
                    Console.WriteLine($"[包含] 路径通配符匹配: {relativePath} 匹配 {pattern}");
                    return true;
                }
            }
            // 精确匹配
            else
            {
                // 检查精确文件名匹配
                if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[包含] 精确文件名匹配: {fileName} 匹配 {pattern}");
                    return true;
                }

                // 检查精确相对路径匹配
                if (relativePath.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[包含] 精确路径匹配: {relativePath} 匹配 {pattern}");
                    return true;
                }
            }
        }

        Console.WriteLine($"[跳过] 文件不匹配任何模式: {relativePath}");
        return false;
    }

    #endregion
}