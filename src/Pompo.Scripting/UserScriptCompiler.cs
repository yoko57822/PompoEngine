using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pompo.Scripting;

public sealed record ScriptSecurityOptions(
    bool AllowFileSystem = false,
    bool AllowNetwork = false,
    bool AllowProcessExecution = false);

public sealed record ScriptCompilationResult(
    bool Success,
    byte[]? AssemblyBytes,
    IReadOnlyList<string> Diagnostics);

public sealed class UserScriptCompiler
{
    private static readonly string[] DefaultBlockedNamespaces =
    [
        "System.IO",
        "System.Net",
        "System.Diagnostics"
    ];

    private static readonly string[] AlwaysBlockedNamespaces =
    [
        "System.Reflection",
        "System.Runtime.Loader"
    ];

    private static readonly (string ContainingType, string MemberName)[] AlwaysBlockedMembers =
    [
        ("System.Type", "GetType"),
        ("System.Activator", "CreateInstance")
    ];

    public ScriptCompilationResult Compile(
        IReadOnlyDictionary<string, string> sources,
        ScriptSecurityOptions securityOptions)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithKind(SourceCodeKind.Regular);

        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Value, parseOptions, path: source.Key))
            .ToArray();

        var references = GetTrustedPlatformReferences();
        var compilation = CSharpCompilation.Create(
            "Pompo.UserScripts",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var securityDiagnostics = ValidateSecurity(compilation, syntaxTrees, securityOptions);
        if (securityDiagnostics.Count > 0)
        {
            return new ScriptCompilationResult(false, null, securityDiagnostics);
        }

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        var diagnostics = emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();

        return emitResult.Success
            ? new ScriptCompilationResult(true, stream.ToArray(), diagnostics)
            : new ScriptCompilationResult(false, null, diagnostics);
    }

    private static IReadOnlyList<string> ValidateSecurity(
        Compilation compilation,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        ScriptSecurityOptions options)
    {
        var blocked = DefaultBlockedNamespaces.ToList();
        if (options.AllowFileSystem)
        {
            blocked.Remove("System.IO");
        }

        if (options.AllowNetwork)
        {
            blocked.Remove("System.Net");
        }

        if (options.AllowProcessExecution)
        {
            blocked.Remove("System.Diagnostics");
        }

        var diagnostics = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var tree in syntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                var symbol = node switch
                {
                    IdentifierNameSyntax or QualifiedNameSyntax or MemberAccessExpressionSyntax or ObjectCreationExpressionSyntax =>
                        model.GetSymbolInfo(node).Symbol,
                    _ => null
                };
                AddBlockedSymbolDiagnostic(tree, node, symbol, blocked, diagnostics);
                AddBlockedSymbolDiagnostic(tree, node, symbol, AlwaysBlockedNamespaces, diagnostics, requiresPermission: false);
                AddBlockedMemberDiagnostic(tree, node, symbol, diagnostics);

                var type = model.GetTypeInfo(node).Type;
                AddBlockedSymbolDiagnostic(tree, node, type, blocked, diagnostics);
                AddBlockedSymbolDiagnostic(tree, node, type, AlwaysBlockedNamespaces, diagnostics, requiresPermission: false);
            }
        }

        return diagnostics.ToArray();
    }

    private static void AddBlockedSymbolDiagnostic(
        SyntaxTree tree,
        SyntaxNode node,
        ISymbol? symbol,
        IReadOnlyList<string> blockedNamespaces,
        ISet<string> diagnostics,
        bool requiresPermission = true)
    {
        if (symbol is null)
        {
            return;
        }

        var namespaceName = GetNamespaceName(symbol);
        var blocked = blockedNamespaces.FirstOrDefault(blockedNamespace =>
            namespaceName.Equals(blockedNamespace, StringComparison.Ordinal) ||
            namespaceName.StartsWith($"{blockedNamespace}.", StringComparison.Ordinal));
        if (blocked is null)
        {
            return;
        }

        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        diagnostics.Add(requiresPermission
            ? $"{tree.FilePath}:{line}: namespace '{blocked}' requires explicit project permission."
            : $"{tree.FilePath}:{line}: namespace '{blocked}' is not allowed in user scripts.");
    }

    private static void AddBlockedMemberDiagnostic(
        SyntaxTree tree,
        SyntaxNode node,
        ISymbol? symbol,
        ISet<string> diagnostics)
    {
        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        var containingType = method.ContainingType?.ToDisplayString();
        if (containingType is null)
        {
            return;
        }

        var blocked = AlwaysBlockedMembers.FirstOrDefault(member =>
            string.Equals(member.ContainingType, containingType, StringComparison.Ordinal) &&
            string.Equals(member.MemberName, method.Name, StringComparison.Ordinal));
        if (blocked == default)
        {
            return;
        }

        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        diagnostics.Add($"{tree.FilePath}:{line}: member '{blocked.ContainingType}.{blocked.MemberName}' is not allowed in user scripts.");
    }

    private static string GetNamespaceName(ISymbol symbol)
    {
        if (symbol is INamespaceSymbol namespaceSymbol)
        {
            return namespaceSymbol.ToDisplayString();
        }

        var type = symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol eventSymbol => eventSymbol.ContainingType,
            _ => symbol.ContainingType
        };

        return (type?.ContainingNamespace ?? symbol.ContainingNamespace)?.ToDisplayString() ?? string.Empty;
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            return [];
        }

        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }
}
