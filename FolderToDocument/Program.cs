using FolderToDocument;

Console.WriteLine("=> 文件夹文档生成器 [v3.0 AI 交互增强版]");
Console.WriteLine("=========================================");

var generator = new FolderDocumentGenerator();

try
{
    // ────────────────────────────────────────────────────────────────────
    // 【步骤 1】指定要扫描的项目根目录路径
    // ────────────────────────────────────────────────────────────────────
    //string folderPath = @"E:\MyCode\C#\MyReview\FolderToDocument";
    //string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoPlatform";
    //string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoPlatformServer";
    //string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPao.SanJieWenDao";
    string folderPath = @"E:\MyCode\C#\MyWork\ZYLuoSanPaoGroupGame";

    // ────────────────────────────────────────────────────────────────────
    // 【步骤 2】配置输出范围：哪些目录/文件需要被包含（支持 Glob 语法）
    //
    // ⚠️ 与 entryClasses 的联动规则：
    //   - entryClasses 不为空时：仅控制【非 .cs 文件】（json/xml/csproj 等）的范围
    //                            .cs 文件的输出由 entryClasses 可达类集合决定
    //   - entryClasses 为空时：  控制【所有文件】的输出范围（常规行为）
    // ────────────────────────────────────────────────────────────────────
    var includedPatterns = new List<string>
    {
        "ZYHuaKuiModule/**",
        "PropPurchaseModule/**",
        //"*.sln",
        //"global.json",
    };

    // ────────────────────────────────────────────────────────────────────
    // 【步骤 3】配置入口类过滤（可选）
    //
    // 填入关心的类名后，生成的文档将只包含这些类及其所有可达的引用类。
    // 留空（Count == 0）则不启用，所有文件按 includedPatterns 正常输出。
    //
    // 配合 entryClassesMaxDepth 可以精确控制向外扩散的层数：
    //   -1 = 无限制，追踪所有间接引用（默认）
    //    0 = 仅保留入口类自身，不展开任何引用
    //    1 = 入口类 + 直接引用的类
    //    N = 最多向外扩散 N 轮
    // ────────────────────────────────────────────────────────────────────
    var entryClasses = new List<string>
    {
        //"HappyFarmCommand",
    };
    int entryClassesMaxDepth = 1;

    // ────────────────────────────────────────────────────────────────────
    // 【步骤 4】配置排除项
    //
    // excludedClasses：精确匹配类名，命中的类在所有模式下均不输出
    // excludedFolders：精确匹配文件夹名（大小写不敏感），整个文件夹下的文件均不输出
    // ────────────────────────────────────────────────────────────────────
    var excludedClasses = new List<string>
    {
        //"FolderDocumentGenerator",
    };
    var excludedFolders = new List<string>
    {
        //"WebImpl",
    };

    // ────────────────────────────────────────────────────────────────────
    // 【步骤 5】配置保留完整实现的标识符（仅 skeleton 模式有效）
    //
    // 默认 skeleton 模式会将所有方法体替换为简短提示注释以节省 Token。
    // 在此列出的标识符将保留完整方法体，支持三种粒度：
    //   "BuildBlockHint"                      → 所有类中同名方法
    //   "SkeletonRewriter"                    → 整个类的所有方法
    //   "FolderDocumentGenerator.StripComments" → 精确到某个类的某个方法
    // ────────────────────────────────────────────────────────────────────
    var preservedMethods = new List<string>
    {
        //"BotUtilsService",
        //"CommonService",
        //"FolderDocumentGenerator.SanitizeSensitiveInfo",
    };

    // ────────────────────────────────────────────────────────────────────
    // 【步骤 6】填写本次交给 AI 处理的业务需求
    //
    // 这里写的内容会原文嵌入到生成的 MD 文档头部，AI 读取后按要求处理代码。
    // ────────────────────────────────────────────────────────────────────
    var myRequirements = new List<string>
    {
        "现在要把花魁商城对应物品的图片上传到本地以供日后调用，用这种方式实现public static string ImageToBase64DataUrl(string imagePath)\n{\n    byte[] imageBytes = File.ReadAllBytes(imagePath);\n    string base64 = Convert.ToBase64String(imageBytes);\n\n    string extension = Path.GetExtension(imagePath).ToLower();\n    string mimeType = extension switch\n    {\n        \".jpg\" or \".jpeg\" => \"image/jpeg\",\n        \".png\" => \"image/png\",\n        \".gif\" => \"image/gif\",\n        \".webp\" => \"image/webp\",\n        _ => \"application/octet-stream\"\n    };\n\n    return $\"data:{mimeType};base64,{base64}\";\n}", 
        "明白代码所用框架及修改目标所需模型有无缺少，如果缺乏必要相关代码，告诉我提供。",
        "不允许修改代码，用自己的理解描述我的需求，我确认后再修改"
        // 请执行以下步骤：
        // 1. 先分析整个文件，列出所有因本次修改而需要同步修改的位置（行号或代码片段）。
        // 2. 然后对列表中的每一个位置，输出修改后的代码及其前后两行（可输出多个这样的代码块）。
        // 3. 不要省略任何位置，即使会输出多个代码块。
       
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
    if (entryClasses.Count > 0)
        Console.WriteLine($"[过滤] 入口类: {string.Join(", ", entryClasses)}");
    Console.WriteLine();

    // ────────────────────────────────────────────────────────────────────
    // 【步骤 7】选择输出模式并执行生成
    //
    // taskMode 可选值：
    //   "optimize"  → 代码优化审阅（默认，输出 Skeleton 以节省 Token）
    //   "debug"     → 运行时异常排查与修复
    //   "explain"   → 代码逻辑讲解（保留完整源码，适合入门分析）
    //   "skeleton"  → 仅输出骨架结构，Token 减少约 60-80%，适合大型项目架构审查
    // ────────────────────────────────────────────────────────────────────
    string finalPath = await generator.GenerateDocumentAsync(
        folderPath,
        null,
        includedPatterns,
        taskMode: "debug",
        customRequirements: myRequirements,
        excludedClasses: excludedClasses,
        preservedMethods: preservedMethods,
        excludedFolders: excludedFolders,
        entryClasses: entryClasses,
        entryClassesMaxDepth: entryClassesMaxDepth
    );

    Console.WriteLine("\n[🎉 成功] 文档已生成！");
    Console.WriteLine($"[📍 文件] {finalPath}");
    Console.WriteLine("\n💡 建议操作：");
    Console.WriteLine("1. 在 myRequirements 列表中填入你的业务需求。");
    Console.WriteLine("2. 重新运行程序，将生成的 MD 文件全选粘贴给 AI（如 Claude/ChatGPT）。");
    Console.WriteLine("3. AI 将自动模仿项目的编码风格为你生成新的功能模块。");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[💥 运行时崩溃] {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}