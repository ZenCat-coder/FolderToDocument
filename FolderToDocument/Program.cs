using FolderToDocument;

Console.WriteLine("=> 文件夹文档生成器 [v3.0 AI 交互增强版]");
Console.WriteLine("=========================================");

var generator = new FolderDocumentGenerator();

try
{
    // 1. 配置：输入你需要扫描的项目路径
    // string folderPath = @"E:\MyCode\FolderToDocument";
    //string folderPath = @"E:\MyScript\ZenlessZoneZero";
    //string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoGroupGame";
    string folderPath = @"E:\MyCode\C#\MyReview\FolderToDocument";


    // 2. 配置：包含模式（推荐只包含主模块，防止 Token 溢出）
    var includedPatterns = new List<string>
    {
        //"ZYIdiomsSolitaireGamesModule/**",
        //"ZYImageGuessIdiomModule/**",
        "FolderToDocument/**",
        //"ZYFishingGameModule/**",
        //"ZYBasisBusinessModule/**",
        //"ZYWireDefuserGameModule/**",
        //"ZYUndercoverGameModule/**",
        //"*.sln",
        //"global.json",
    };

    // 路径合法性校验
    if (!Directory.Exists(folderPath))
    {
        Console.WriteLine($"[错误] 找不到路径: {folderPath}");
        return;
    }

    string projectName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
    Console.WriteLine($"[任务] 分析项目: {projectName}");
    Console.WriteLine($"[模式] 包含规则: {string.Join(", ", includedPatterns)}");
    Console.WriteLine();

    // 默认输出至: 你的工作目录/Md/项目名/项目名.md
    // 3. 配置：自定义 AI 专项要求 (这些会直接出现在 MD 文件的头部指令中)
    var myRequirements = new List<string>
    {
        "能否再优化一下，让解释更详细一点。比如"
    };

    // 4. 执行生成
    // 传入模式 (optimize 、explain 或 debug) 以及自定义要求
    string finalPath = await generator.GenerateDocumentAsync(
        folderPath,
        null,
        includedPatterns,
        taskMode: "optimize",
        customRequirements: myRequirements
    );

    // 4. 结果反馈
    Console.WriteLine("\n[🎉 成功] 文档已针对 AI 进行了深度优化并生成！");
    Console.WriteLine($"[📍 文件] {finalPath}");
    Console.WriteLine("\n💡 建议操作：");
    Console.WriteLine("1. 使用 VS Code 打开此 MD 文件预览效果。");
    Console.WriteLine("2. 全选内容并粘贴给 AI (如 ChatGPT 或 Claude)。");
    Console.WriteLine("3. 由于带有了【行号】和【指令集】，你可以直接命令 AI 修改具体代码块。");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[💥 运行时崩溃] {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

// Console.WriteLine("\n按任意键退出工具...");
// Console.ReadKey();