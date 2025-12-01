using FolderToDocument;

Console.WriteLine("=> 文件夹文档生成器");
Console.WriteLine("====================");

var generator = new FolderDocumentGenerator();


try
{
    // 设置要扫描的项目文件夹路径
    string folderPath = @"E:\MyCode\C#\MyWork\ZYCsjOrderReceiptService";

    // 设置自定义输出路径
    string outputPath = "";

    // 设置包含模式 - 使用更简单的模式
    var includedPatterns = new List<string>
    {
        "ZYCsjOrderReceiptBusinessModule/**", // 包含整个业务模块
    };

    //  示例2：包含业务模块和项目结构文件
    // var includedPatterns = new List<string>
    // {
    //     "ZYCsjOrderReceiptBusinessModule/**", // 业务模块所有文件
    //     "*.sln", // 解决方案文件
    //     "**/*.csproj" // 所有项目文件
    // };
    
    // 验证输入目录是否存在
    if (!Directory.Exists(folderPath))
    {
        Console.WriteLine($"[错误] 指定的目录不存在: {folderPath}");
        Console.WriteLine("请修改 Program.cs 中的 folderPath 变量为有效的项目路径");
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
        return;
    }

    // 显示项目信息
    string projectName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
    Console.WriteLine($"[项目] 项目名称: {projectName}");
    Console.WriteLine($"[路径] 项目路径: {folderPath}");
    Console.WriteLine($"[输出] 输出路径: {outputPath}");
    Console.WriteLine($"[过滤] 包含模式: {string.Join(", ", includedPatterns)}");
    Console.WriteLine();

    // 生成文档 - 传入包含模式
    string finalOutputPath = await generator.GenerateDocumentAsync(folderPath, outputPath, includedPatterns);

    // 显示成功信息和使用说明
    Console.WriteLine();
    Console.WriteLine("[完成] 文档生成完成！");
    Console.WriteLine();
    Console.WriteLine("使用说明:");
    Console.WriteLine($"1. 文档已保存到: {finalOutputPath}");
    Console.WriteLine($"2. 您可以在文件管理器中打开此文件");
    Console.WriteLine($"3. 推荐使用 Markdown 编辑器（如 VS Code、Typora）查看");
    Console.WriteLine($"4. 文档包含筛选后的项目结构和代码内容");
    Console.WriteLine($"5. 配置文件已自动脱敏处理，敏感信息已隐藏");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"[异常] 程序执行过程中发生错误:");
    Console.WriteLine($"错误类型: {ex.GetType().Name}");
    Console.WriteLine($"错误信息: {ex.Message}");

    if (ex is UnauthorizedAccessException)
    {
        Console.WriteLine();
        Console.WriteLine("解决方案建议:");
        Console.WriteLine("1. 以管理员身份运行此程序");
        Console.WriteLine("2. 检查文件夹权限设置");
        Console.WriteLine("3. 尝试将项目复制到其他位置再生成文档");
    }
}