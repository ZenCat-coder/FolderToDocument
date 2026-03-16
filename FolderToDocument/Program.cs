using FolderToDocument;

Console.WriteLine("=> 文件夹文档生成器 [v3.0 AI 交互增强版]");
Console.WriteLine("=========================================");

var generator = new FolderDocumentGenerator();

try
{
    // 1. 配置：输入你需要扫描的项目路径
    //string folderPath = @"E:\MyCode\C#\MyReview\FolderToDocument";
    //string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoPlatform";
    //string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoPlatformServer";
    //string folderPath = @"E:\MyCode\C#\MyReview\DelegateTest";
    string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoGroupGame";


    // 2. 配置：包含模式（推荐只包含主模块，防止 Token 溢出）
    var includedPatterns = new List<string>
    {
        //"PropPurchaseModule/**",
        //"ZYPropsModule/**",
        "ZYSignItDailyModule/**",
        //"*.sln",
        //"global.json",
    };

    // 3. 配置：需要从输出中排除的类名（精确匹配，所有模式均生效）
    var excludedClasses = new List<string>
    {
        //"Tests", // 示例：不输出此内嵌类
        // "FolderDocumentGenerator",
    };
    // 新增：排除整个文件夹（文件夹名精确匹配，大小写不敏感，该文件夹下所有文件均不输出）
    var excludedFolders = new List<string>
    {
        "Tests"
    };
    

    // 4. 配置：skeleton 模式下需要保留完整实现的标识符
//    支持三种粒度：
//      "BuildBlockHint"              → 所有类中名为 BuildBlockHint 的方法
//      "SkeletonRewriter"            → 整个 SkeletonRewriter 类的所有方法
//      "FolderDocumentGenerator.StripComments" → 精确到某个类的某个方法
    var preservedMethods = new List<string>
    {
        //"BuildBlockHint",
        // "SkeletonRewriter",
        // "FolderDocumentGenerator.SanitizeSensitiveInfo",
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
    // 5. 配置：自定义 AI 专项要求 (这些会直接出现在 MD 文件的头部指令中)
    var myRequirements = new List<string>
    {
        "mock都是根据代码逻辑来测试的，如果我代码逻辑不对，那测试的也不对，比如这个抽签隔日零点没有刷新，只有用实战的方式测出来，我怕有其他bug。这mock好像也不是万能的",
        "先不修改代码，明白代码所用框架及修改目标所需要用的模型有无缺少，如果缺乏必要相关代码，告诉我提供。",
        "使用中文回答"
    };

    // 6. 执行生成
    // 传入模式 (optimize 、explain 或 debug) 以及自定义要求
    // 新增 skeleton 模式，Token 减少约 60-80%：
    string finalPath = await generator.GenerateDocumentAsync(
        folderPath,
        null,
        includedPatterns,
        taskMode: "debug",
        customRequirements: myRequirements,
        excludedClasses: excludedClasses,
        preservedMethods: preservedMethods,
        excludedFolders: excludedFolders // <--- 原因: 接入新参数
    );

    // 7. 结果反馈
    Console.WriteLine("\n[🎉 成功] 文档已针对 AI 进行了深度优化并生成！");
    Console.WriteLine($"[📍 文件] {finalPath}");
    Console.WriteLine("\n💡 建议操作：");
    Console.WriteLine("1. 使用 VS Code 打开此 MD 文件预览效果。");
    Console.WriteLine("2. 全选内容并粘贴给 AI (如 ChatGPT 或 Claude)。");
    Console.WriteLine("3. 由于文件包含完整【指令集】和清晰的结构，你可以直接命令 AI 修改具体代码块。");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[💥 运行时崩溃] {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}