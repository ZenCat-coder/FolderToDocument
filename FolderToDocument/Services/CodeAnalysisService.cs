using System.Collections.Frozen;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using FolderToDocument.Interfaces;

namespace FolderToDocument.Services;

/// <summary>代码分析服务实现</summary>
public class CodeAnalysisService : ICodeAnalysisService
{
    private static readonly FrozenSet<string> ExcludedFolders = new[]
    {
        "bin", "obj", ".vs", ".git", "node_modules", "packages", "Debug", "Release", ".idea", "dist", "build",
        "__pycache__", "Properties"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public async Task<ClassGraphResult> BuildClassReferenceGraphAsync(
        string rootPath,
        List<string> excludedFolders,
        IFileSystemService fileSystem)
    {
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 20
        };

        var csFiles = fileSystem.EnumerateFiles(rootPath, "*.cs", enumerationOptions)
            .Where(f =>
            {
                var parts = f.Split(Path.DirectorySeparatorChar);
                return !ExcludedFolders.Any(ef => parts.Contains(ef, StringComparer.OrdinalIgnoreCase));
            })
            .Where(f => excludedFolders == null ||
                        !excludedFolders.Any(ef =>
                            f.Split(Path.DirectorySeparatorChar)
                                .Contains(ef, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var allTypeNames = new HashSet<string>(StringComparer.Ordinal);
        var fileSourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in csFiles)
        {
            try
            {
                string source = await File.ReadAllTextAsync(file, Encoding.UTF8);
                fileSourceMap[file] = source;

                var tree = CSharpSyntaxTree.ParseText(source);
                var root = await tree.GetRootAsync();

                foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                    allTypeNames.Add(node.Identifier.ValueText);

                foreach (var node in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                    allTypeNames.Add(node.Identifier.ValueText);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var implementsMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (_, source) in fileSourceMap)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(source);
                var root = await tree.GetRootAsync();

                var extractor = new ClassReferenceExtractor(allTypeNames);
                extractor.Visit(root);

                foreach (var (typeName, refs) in extractor.ClassReferences)
                {
                    if (!graph.TryGetValue(typeName, out var existing))
                    {
                        existing = new HashSet<string>(StringComparer.Ordinal);
                        graph[typeName] = existing;
                    }
                    foreach (var refName in refs)
                        existing.Add(refName);
                }

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (classDecl.BaseList == null) continue;

                    string className = classDecl.Identifier.ValueText;
                    if (!implementsMap.TryGetValue(className, out var baseTypes))
                    {
                        baseTypes = new HashSet<string>(StringComparer.Ordinal);
                        implementsMap[className] = baseTypes;
                    }

                    foreach (var baseTypeSyntax in classDecl.BaseList.Types)
                    {
                        string baseName = baseTypeSyntax.Type switch
                        {
                            SimpleNameSyntax simple => simple.Identifier.ValueText,
                            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                            _ => string.Empty
                        };

                        if (!string.IsNullOrEmpty(baseName) && allTypeNames.Contains(baseName))
                            baseTypes.Add(baseName);
                    }
                }
            }
            catch
            {
            }
        }

        return new ClassGraphResult(graph, implementsMap);
    }

    private sealed class ClassReferenceExtractor : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _knownTypeNames;
        private string _currentType;

        public Dictionary<string, HashSet<string>> ClassReferences { get; } = new(StringComparer.Ordinal);

        public ClassReferenceExtractor(IEnumerable<string> knownTypeNames)
        {
            _knownTypeNames = new HashSet<string>(knownTypeNames, StringComparer.Ordinal);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            string prev = _currentType;
            _currentType = node.Identifier.ValueText;
            EnsureNode(_currentType);
            base.VisitClassDeclaration(node);
            _currentType = prev;
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            string prev = _currentType;
            _currentType = node.Identifier.ValueText;
            EnsureNode(_currentType);
            base.VisitInterfaceDeclaration(node);
            _currentType = prev;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            RecordReference(node.Identifier.ValueText);
            base.VisitIdentifierName(node);
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            RecordReference(node.Identifier.ValueText);
            base.VisitGenericName(node);
        }

        private void EnsureNode(string typeName)
        {
            if (!ClassReferences.ContainsKey(typeName))
                ClassReferences[typeName] = new HashSet<string>(StringComparer.Ordinal);
        }

        private void RecordReference(string name)
        {
            if (_currentType == null) return;
            if (name == _currentType) return;
            if (!_knownTypeNames.Contains(name)) return;

            ClassReferences[_currentType].Add(name);
        }
    }

    public async Task<(string FilteredSource, bool AllRemoved)> KeepOnlyReachableClassesAsync(
        string source,
        HashSet<string> reachableClasses)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();

        var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        var allInterfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().ToList();

        if (allClasses.Count == 0 && allInterfaces.Count == 0)
            return (source, false);

        bool anyReachable =
            allClasses.Any(c => reachableClasses.Contains(c.Identifier.ValueText)) ||
            allInterfaces.Any(i => reachableClasses.Contains(i.Identifier.ValueText));

        if (!anyReachable)
            return (source, true);

        var rewriter = new ClassKeepRewriter(reachableClasses);
        var newRoot = rewriter.Visit(root);
        return (newRoot.ToFullString(), false);
    }

    private sealed class ClassKeepRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _keepNames;

        public ClassKeepRewriter(IEnumerable<string> keepNames)
        {
            _keepNames = new HashSet<string>(keepNames, StringComparer.Ordinal);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!_keepNames.Contains(node.Identifier.ValueText))
                return null;
            return base.VisitClassDeclaration(node);
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (!_keepNames.Contains(node.Identifier.ValueText))
                return null;
            return base.VisitInterfaceDeclaration(node);
        }
    }

    public async Task<(string FilteredSource, bool AllExcluded)> RemoveExcludedClassesAsync(
        string source,
        IReadOnlyCollection<string> excludedClasses)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();

        int originalMatchCount = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Count(c => excludedClasses.Contains(c.Identifier.ValueText));

        var rewriter = new ClassRemovalRewriter(excludedClasses);
        var newRoot = rewriter.Visit(root);
        string filtered = newRoot.ToFullString();

        int remainingClassCount = newRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Count();

        bool allExcluded =
            originalMatchCount > 0 &&
            remainingClassCount == 0;

        return (filtered, allExcluded);
    }

    private sealed class ClassRemovalRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _excludedNames;

        public ClassRemovalRewriter(IEnumerable<string> excludedNames)
        {
            _excludedNames = new HashSet<string>(excludedNames, StringComparer.Ordinal);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (_excludedNames.Contains(node.Identifier.ValueText))
                return null;
            return base.VisitClassDeclaration(node);
        }
    }

    public async Task<string> ExtractCSharpSkeletonAsync(
        string source,
        IReadOnlyCollection<string> preservedMethods = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();
        var rewriter = new SkeletonRewriter(preservedMethods);
        var skeletonRoot = rewriter.Visit(root);
        return skeletonRoot.ToFullString();
    }

    private sealed class SkeletonRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _preserved;
        private string _currentClassName;

        private static readonly HashSet<string> LinqOperatorNames = new(StringComparer.Ordinal)
        {
            "Where", "Select", "SelectMany", "GroupBy", "OrderBy", "OrderByDescending",
            "ThenBy", "Join", "Any", "All", "First", "FirstOrDefault", "Single",
            "SingleOrDefault", "Count", "Sum", "Min", "Max", "Distinct", "ToList",
            "ToArray", "ToDictionary", "ToFrozenSet", "Aggregate", "Skip", "Take",
            "Concat", "Zip", "Except", "Intersect", "Union"
        };

        public SkeletonRewriter(IEnumerable<string> preservedMethods = null)
        {
            _preserved = preservedMethods != null
                ? new HashSet<string>(preservedMethods, StringComparer.Ordinal)
                : [];
        }

        private bool ShouldPreserve(string simpleName)
        {
            if (_preserved.Count == 0) return false;
            if (_preserved.Contains(simpleName)) return true;
            if (_currentClassName != null &&
                _preserved.Contains(_currentClassName + "." + simpleName)) return true;
            return false;
        }

        private static string BuildBlockHint(BlockSyntax body)
        {
            if (body == null || body.Statements.Count == 0) return "/* empty */";

            var parts = new List<string>(7);

            var calls = body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Parent is not ArgumentSyntax)
                .Select(inv =>
                {
                    string receiver = inv.Expression.ToString();
                    string args = string.Join(", ", inv.ArgumentList.Arguments.Select(a => a.ToString()));
                    string full = args.Length > 0 ? $"{receiver}({args})" : $"{receiver}()";
                    return full.Length <= 80 ? full : $"{receiver}(...)";
                })
                .Where(n => n.Length > 0 && !LinqOperatorNames.Contains(
                    n.Contains('(') ? n[..n.IndexOf('(')].Split('.').Last() : n))
                .Distinct()
                .Take(6);
            var callStr = string.Join(", ", calls);
            if (callStr.Length > 0) parts.Add($"calls: {callStr}");

            var linqOps = body.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Select(m => m.Name.Identifier.ValueText)
                .Where(n => LinqOperatorNames.Contains(n))
                .Distinct()
                .Take(5);
            var linqStr = string.Join("→", linqOps);
            if (linqStr.Length > 0) parts.Add($"linq: {linqStr}");

            var newTypes = body.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Select(o => o.Type.ToString())
                .Where(t => t.Length is > 0 and <= 35)
                .Distinct()
                .Take(3);
            var newStr = string.Join(", ", newTypes);
            if (newStr.Length > 0) parts.Add($"new: {newStr}");

            var foreachSources = body.DescendantNodes()
                .OfType<ForEachStatementSyntax>()
                .Select(f => f.Expression.ToString().Trim())
                .Where(e => e.Length is > 0 and <= 30)
                .Distinct()
                .Take(2);
            var foreachStr = string.Join(", ", foreachSources);
            if (foreachStr.Length > 0) parts.Add($"foreach: {foreachStr}");

            var conditions = body.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Select(i => i.Condition.ToString().Trim())
                .Where(c => c.Length is > 0 and <= 45)
                .Distinct()
                .Take(2);
            var condStr = string.Join(" | ", conditions);
            if (condStr.Length > 0) parts.Add($"if: {condStr}");

            var returns = body.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Where(r => r.Expression != null)
                .Select(r => r.Expression!.ToString().Trim())
                .Where(e => e.Length is > 0 and <= 60)
                .Distinct()
                .Take(2);
            var retStr = string.Join(" | ", returns);
            if (retStr.Length > 0) parts.Add($"→ {retStr}");

            var throwTypes = body.DescendantNodes()
                .Select<SyntaxNode, string>(n => n switch
                {
                    ThrowStatementSyntax ts => (ts.Expression as ObjectCreationExpressionSyntax)?.Type.ToString() ?? "",
                    ThrowExpressionSyntax te => (te.Expression as ObjectCreationExpressionSyntax)?.Type.ToString() ?? "",
                    _ => ""
                })
                .Where(t => t.Length > 0)
                .Distinct()
                .Take(2);
            var throwStr = string.Join(", ", throwTypes);
            if (throwStr.Length > 0) parts.Add($"throws: {throwStr}");

            return parts.Count > 0 ? $"/* {string.Join(" | ", parts)} */" : "/* ... */";
        }

        private static string BuildExpressionHint(ArrowExpressionClauseSyntax arrow)
        {
            var expr = arrow.Expression.ToString().Trim().Replace("*/", "*\\/");
            if (expr.Length > 120) expr = expr[..120] + "...";
            return $"/* => {expr} */";
        }

        private static BlockSyntax WrapInStubBlock(string hint) =>
            SyntaxFactory.Block()
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                        .WithTrailingTrivia(SyntaxFactory.TriviaList(
                            SyntaxFactory.Space,
                            SyntaxFactory.Comment(hint),
                            SyntaxFactory.Space)))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        private static BlockSyntax StubFromBlock(BlockSyntax body) => WrapInStubBlock(BuildBlockHint(body));

        private static BlockSyntax StubFromArrow(ArrowExpressionClauseSyntax a) =>
            WrapInStubBlock(BuildExpressionHint(a));

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText))
                return node;

            var prev = _currentClassName;
            _currentClassName = node.Identifier.ValueText;
            var result = base.VisitClassDeclaration(node);
            _currentClassName = prev;
            return result;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText)) return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText)) return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitConstructorDeclaration(node);
        }

        public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Identifier.ValueText)) return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitDestructorDeclaration(node);
        }

        public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.Keyword.ValueText))
                return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitAccessorDeclaration(node);
        }

        public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            if (ShouldPreserve(node.OperatorToken.ValueText))
                return node;

            if (node.Body != null)
                return node.WithBody(StubFromBlock(node.Body));
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default)
                    .WithBody(StubFromArrow(node.ExpressionBody));
            return base.VisitOperatorDeclaration(node);
        }
    }

    public async Task<string> ExtractUltraSkeletonAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();
        var rewriter = new UltraSkeletonRewriter();
        var result = rewriter.Visit(root);
        return result.ToFullString();
    }

    private sealed class UltraSkeletonRewriter : CSharpSyntaxRewriter
    {
        private static readonly BlockSyntax EmptyBlock =
            SyntaxFactory.Block()
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                        .WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space)))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            token = base.VisitToken(token);

            var filteredLeading = token.LeadingTrivia
                .Where(t => !t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                            && !t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                .ToSyntaxTriviaList();

            return token.WithLeadingTrivia(filteredLeading);
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Body != null)
                return node.WithBody(EmptyBlock).WithLeadingTrivia(node.GetLeadingTrivia());
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(EmptyBlock)
                    .WithLeadingTrivia(node.GetLeadingTrivia());
            return node;
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node.Body != null)
                return node.WithBody(EmptyBlock);
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default).WithBody(EmptyBlock);
            return node;
        }

        public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (node.Body != null)
                return node.WithBody(EmptyBlock);
            if (node.ExpressionBody != null)
                return node.WithExpressionBody(null).WithSemicolonToken(default).WithBody(EmptyBlock);
            return node;
        }
    }
}