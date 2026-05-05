using System.Text.Json;
using System.Xml.Linq;

namespace Pompo.Build;

public sealed class RepositoryDoctorService
{
    private static readonly string[] RequiredFiles =
    [
        "global.json",
        "PompoEngine.slnx",
        "Directory.Build.props",
        ".editorconfig",
        ".gitattributes",
        ".gitignore",
        ".ignore",
        "LICENSE",
        "README.md",
        "CHANGELOG.md",
        "MAINTAINERS.md",
        "CONTRIBUTING.md",
        "CODE_OF_CONDUCT.md",
        "SUPPORT.md",
        "SECURITY.md",
        ".github/dependabot.yml",
        ".github/workflows/ci.yml",
        ".github/workflows/codeql.yml",
        ".github/workflows/dependency-review.yml",
        ".github/workflows/docs.yml",
        ".github/workflows/release.yml",
        ".github/ISSUE_TEMPLATE/bug_report.md",
        ".github/ISSUE_TEMPLATE/feature_request.md",
        ".github/ISSUE_TEMPLATE/config.yml",
        ".github/pull_request_template.md",
        "scripts/check-release-gates.sh",
        "scripts/check-release-gates.ps1",
        "docs/GETTING_STARTED.md",
        "docs/RUN_AND_USE.md",
        "docs/DEVELOPMENT.md",
        "docs/TROUBLESHOOTING.md",
        "docs/ARCHITECTURE.md",
        "docs/PRODUCTION_AUDIT.md",
        "docs/ROADMAP.md",
        "docs/COMPATIBILITY.md",
        "docs/SCRIPTING.md",
        "docs/RELEASING.md",
        "docs/OPEN_SOURCE_RELEASE_CHECKLIST.md"
    ];

    private static readonly string[] SolutionProjects =
    [
        "src/Pompo.Build/Pompo.Build.csproj",
        "src/Pompo.Cli/Pompo.Cli.csproj",
        "src/Pompo.Core/Pompo.Core.csproj",
        "src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj",
        "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj",
        "src/Pompo.Scripting/Pompo.Scripting.csproj",
        "src/Pompo.VisualScripting/Pompo.VisualScripting.csproj",
        "tests/Pompo.Tests/Pompo.Tests.csproj"
    ];

    private static readonly string[] RequiredIgnorePatterns =
    [
        "bin/",
        "obj/",
        "TestResults/",
        ".DS_Store",
        "Thumbs.db",
        "Desktop.ini",
        "artifacts/",
        "dist/"
    ];

    private static readonly string[] ForbiddenWorkspaceFileNames =
    [
        ".DS_Store",
        "Thumbs.db",
        "Desktop.ini"
    ];

    private static readonly string[] RequiredGitAttributesPatterns =
    [
        "* text=auto eol=lf",
        "*.cs text eol=lf",
        "*.json text eol=lf",
        "*.md text eol=lf",
        "*.sh text eol=lf",
        "*.ps1 text eol=lf",
        "*.png binary",
        "*.wav binary",
        "*.zip binary"
    ];

    public async Task<ProjectDoctorResult> InspectAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var diagnostics = new List<ProjectDoctorDiagnostic>();
        ValidateRequiredFiles(repositoryRoot, diagnostics);
        ValidateExecutableScripts(repositoryRoot, diagnostics);
        ValidateNoForbiddenWorkspaceFiles(repositoryRoot, diagnostics);
        await ValidateIgnoreFilesAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateGitAttributesAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateGlobalJsonAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateDirectoryBuildPropsAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateSolutionAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateProjectBoundariesAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateWorkflowTextAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateReleaseGateScriptAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        await ValidateDocumentationLinksAsync(repositoryRoot, diagnostics, cancellationToken).ConfigureAwait(false);

        return new ProjectDoctorResult(diagnostics);
    }

    private static void ValidateRequiredFiles(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        foreach (var relativePath in RequiredFiles)
        {
            if (!File.Exists(Path.Combine(repositoryRoot, relativePath)))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO001",
                    $"Required repository file '{relativePath}' does not exist.",
                    relativePath));
            }
        }
    }

    private static void ValidateExecutableScripts(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string relativePath = "scripts/check-release-gates.sh";
        var path = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        if ((mode & UnixFileMode.UserExecute) == 0)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO011",
                $"Repository script '{relativePath}' must be executable on Unix-like systems.",
                relativePath));
        }
    }

    private static void ValidateNoForbiddenWorkspaceFiles(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        var root = Path.GetFullPath(repositoryRoot);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (!ForbiddenWorkspaceFileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO012",
                $"Repository workspace must not contain generated OS metadata file '{name}'.",
                Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/')));
        }
    }

    private static async Task ValidateIgnoreFilesAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var relativePath in new[] { ".gitignore", ".ignore" })
        {
            var path = Path.Combine(repositoryRoot, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            foreach (var pattern in RequiredIgnorePatterns)
            {
                if (!text.Contains(pattern, StringComparison.Ordinal))
                {
                    diagnostics.Add(new ProjectDoctorDiagnostic(
                        "REPO009",
                        $"Repository ignore file '{relativePath}' must exclude generated output '{pattern}'.",
                        relativePath));
                }
            }
        }
    }

    private static async Task ValidateGlobalJsonAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var relativePath = "global.json";
        var path = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var sdk = json.RootElement.TryGetProperty("sdk", out var sdkElement)
                ? sdkElement
                : default;
            var version = sdk.ValueKind == JsonValueKind.Object && sdk.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;
            var rollForward = sdk.ValueKind == JsonValueKind.Object && sdk.TryGetProperty("rollForward", out var rollForwardElement)
                ? rollForwardElement.GetString()
                : null;

            if (!string.Equals(version, "10.0.100", StringComparison.Ordinal) ||
                !string.Equals(rollForward, "disable", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO002",
                    "global.json must pin .NET SDK 10.0.100 with rollForward disabled.",
                    relativePath));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO002",
                $"global.json could not be loaded: {ex.Message}",
                relativePath));
        }
    }

    private static async Task ValidateGitAttributesAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        const string relativePath = ".gitattributes";
        var path = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        foreach (var pattern in RequiredGitAttributesPatterns)
        {
            if (!text.Contains(pattern, StringComparison.Ordinal))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO010",
                    $"Repository attributes file must contain '{pattern}'.",
                    relativePath));
            }
        }
    }

    private static async Task ValidateDirectoryBuildPropsAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var relativePath = "Directory.Build.props";
        var document = await LoadXmlAsync(repositoryRoot, relativePath, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return;
        }

        RequireProperty(document, "TargetFramework", "net10.0", relativePath, diagnostics);
        RequireProperty(document, "ImplicitUsings", "enable", relativePath, diagnostics);
        RequireProperty(document, "Nullable", "enable", relativePath, diagnostics);
        RequireProperty(document, "TreatWarningsAsErrors", "true", relativePath, diagnostics);
        RequireProperty(document, "Deterministic", "true", relativePath, diagnostics);
        RequireProperty(document, "PackageLicenseExpression", "MIT", relativePath, diagnostics);
        RequireProperty(document, "RepositoryType", "git", relativePath, diagnostics);
        RequireNonEmptyProperty(document, "VersionPrefix", relativePath, diagnostics);
        RequireNonEmptyProperty(document, "Authors", relativePath, diagnostics);
        RequireNonEmptyProperty(document, "Description", relativePath, diagnostics);
    }

    private static async Task ValidateSolutionAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var relativePath = "PompoEngine.slnx";
        var document = await LoadXmlAsync(repositoryRoot, relativePath, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return;
        }

        var projectPaths = document
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value.Replace('\\', '/'))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var project in SolutionProjects)
        {
            if (!projectPaths.Contains(project))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO004",
                    $"Solution must include '{project}'.",
                    relativePath));
            }
        }
    }

    private static async Task ValidateProjectBoundariesAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        await RequireNoReferencesAsync(
                repositoryRoot,
                "src/Pompo.Core/Pompo.Core.csproj",
                disallowedProjectFragments: ["Pompo."],
                disallowedPackagePrefixes: [string.Empty],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireNoReferencesAsync(
                repositoryRoot,
                "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj",
                disallowedProjectFragments: ["Pompo.Editor", "Pompo.Build", "Pompo.Cli"],
                disallowedPackagePrefixes: ["Avalonia"],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireNoReferencesAsync(
                repositoryRoot,
                "src/Pompo.VisualScripting/Pompo.VisualScripting.csproj",
                disallowedProjectFragments: ["Pompo.Editor", "Pompo.Runtime", "Pompo.Build", "Pompo.Cli"],
                disallowedPackagePrefixes: ["Avalonia", "FNA"],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireExecutableNotPackableAsync(repositoryRoot, "src/Pompo.Cli/Pompo.Cli.csproj", diagnostics, cancellationToken)
            .ConfigureAwait(false);
        await RequireExecutableNotPackableAsync(repositoryRoot, "src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj", diagnostics, cancellationToken)
            .ConfigureAwait(false);
        await RequireExecutableNotPackableAsync(repositoryRoot, "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj", diagnostics, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ValidateWorkflowTextAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        await RequireTextTokensAsync(
                repositoryRoot,
                ".github/workflows/ci.yml",
                [
                    "permissions:",
                    "contents: read",
                    "dotnet restore PompoEngine.slnx",
                    "dotnet build PompoEngine.slnx --no-restore",
                    "dotnet test PompoEngine.slnx --no-build",
                    "docs site --root . --output artifacts/docs-site --json",
                    "doctor --repository --root .",
                    "init --path \"$project_dir\" --name SmokeVN --template sample --json",
                    "asset import --project \"$project_dir\" --file \"$asset_file\" --type Image --asset-id smoke-bg --json",
                    "asset verify --project \"$project_dir\" --json",
                    "localization report --project \"$project_dir\" --json",
                    "doctor --project",
                    "build verify",
                    "release verify",
                    "--require-smoke-tested-locales",
                    "--require-self-contained"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                ".github/workflows/release.yml",
                [
                    "permissions:",
                    "contents: read",
                    "contents: write",
                    "tags:",
                    "Windows",
                    "MacOS",
                    "Linux",
                    "doctor --repository --root .",
                    "init --path $projectDir --name PompoEngineSample --template sample --json",
                    "localization report --project $projectDir --json",
                    "build verify",
                    "release package",
                    "release verify",
                    "release sign",
                    "verify-signature",
                    "draft: true"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                ".github/workflows/docs.yml",
                [
                    "docs site --root . --output artifacts/docs-site --json",
                    "actions/configure-pages",
                    "actions/upload-pages-artifact",
                    "actions/deploy-pages",
                    "github-pages"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                ".github/workflows/dependency-review.yml",
                [
                    "pull_request:",
                    "permissions:",
                    "contents: read",
                    "pull-requests: read",
                    "actions/dependency-review-action@v4",
                    "fail-on-severity: high"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                ".github/workflows/codeql.yml",
                [
                    "github/codeql-action/init@v4",
                    "github/codeql-action/analyze@v4",
                    "languages: csharp",
                    "security-events: write",
                    "dotnet build PompoEngine.slnx --no-restore"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                ".github/dependabot.yml",
                [
                    "version: 2",
                    "package-ecosystem: nuget",
                    "package-ecosystem: github-actions",
                    "interval: weekly"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ValidateReleaseGateScriptAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        await RequireTextTokensAsync(
                repositoryRoot,
                "scripts/check-release-gates.sh",
                [
                    "set -euo pipefail",
                    "dotnet restore PompoEngine.slnx",
                    "dotnet build PompoEngine.slnx --no-restore",
                    "dotnet test PompoEngine.slnx --no-build",
                    "version --json",
                    "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json",
                    "docs site --root . --output artifacts/docs-site --json",
                    "doctor --repository --root .",
                    "--validate-runtime",
                    "Release gates passed"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "scripts/check-release-gates.ps1",
                [
                    "$ErrorActionPreference = \"Stop\"",
                    "Set-StrictMode -Version Latest",
                    "dotnet restore PompoEngine.slnx",
                    "dotnet build PompoEngine.slnx --no-restore",
                    "dotnet test PompoEngine.slnx --no-build",
                    "version --json",
                    "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json",
                    "docs site --root . --output artifacts/docs-site --json",
                    "doctor --repository --root .",
                    "--validate-runtime",
                    "Release gates passed"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ValidateDocumentationLinksAsync(
        string repositoryRoot,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        await RequireTextTokensAsync(
                repositoryRoot,
                "README.md",
                [
                    "LICENSE",
                    "docs/GETTING_STARTED.md",
                    "docs/RUN_AND_USE.md",
                    "docs/DEVELOPMENT.md",
                    "docs/TROUBLESHOOTING.md",
                    "docs/ARCHITECTURE.md",
                    "CONTRIBUTING.md",
                    "CODE_OF_CONDUCT.md",
                    "SUPPORT.md",
                    "SECURITY.md",
                    "docs/OPEN_SOURCE_RELEASE_CHECKLIST.md",
                    "docs/PRODUCTION_AUDIT.md",
                    "docs/ROADMAP.md",
                    "docs/COMPATIBILITY.md",
                    "docs/SCRIPTING.md",
                    "docs/RELEASING.md",
                    "CHANGELOG.md",
                    "MAINTAINERS.md",
                    "scripts/check-release-gates.sh",
                    "scripts/check-release-gates.ps1",
                    "docs site",
                    "not 1.0 yet"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/DEVELOPMENT.md",
                [
                    "scripts/check-release-gates.sh",
                    "scripts/check-release-gates.ps1",
                    "Project Boundaries",
                    "Pompo.Core",
                    "Pompo.Runtime.Fna",
                    "Pompo.VisualScripting",
                    "DocumentationSiteService",
                    "RepositoryDoctorService",
                    ".DS_Store"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/OPEN_SOURCE_RELEASE_CHECKLIST.md",
                [
                    "dotnet build PompoEngine.slnx --no-restore",
                    "dotnet test PompoEngine.slnx --no-build",
                    "scripts/check-release-gates.sh",
                    "scripts/check-release-gates.ps1",
                    "docs site --root . --output artifacts/docs-site --json",
                    "Release Gates",
                    "docs/PRODUCTION_AUDIT.md",
                    "Packaging Gates"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/PRODUCTION_AUDIT.md",
                [
                    "Prompt-To-Artifact Checklist",
                    "Required Gates",
                    "scripts/check-release-gates.sh",
                    "scripts/check-release-gates.ps1",
                    "Completion Assessment",
                    "remaining production risks"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/COMPATIBILITY.md",
                [
                    "schemaVersion",
                    "CHANGELOG.md",
                    "PompoGraphIR",
                    "release manifest",
                    "scripting API",
                    "JSON output"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/SCRIPTING.md",
                [
                    "PompoCommandNode",
                    "PompoConditionNode",
                    "PompoRuntimeContext",
                    "PompoNodeInput",
                    "scriptPermissions",
                    "Pompo.UserScripts.dll"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/ARCHITECTURE.md",
                [
                    "Project Boundaries",
                    "Authoring Data Flow",
                    "Build Flow",
                    "Release Flow",
                    "Runtime Flow",
                    "Architectural Rules"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);

        await RequireTextTokensAsync(
                repositoryRoot,
                "docs/TROUBLESHOOTING.md",
                [
                    "Project Does Not Open",
                    "Graph Preview Fails",
                    "User Scripts Fail to Compile",
                    "Build Fails",
                    "Release Verification Fails",
                    "Documentation Site Fails"
                ],
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task RequireNoReferencesAsync(
        string repositoryRoot,
        string relativePath,
        IReadOnlyList<string> disallowedProjectFragments,
        IReadOnlyList<string> disallowedPackagePrefixes,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var document = await LoadXmlAsync(repositoryRoot, relativePath, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return;
        }

        foreach (var include in document.Descendants("ProjectReference").Select(element => element.Attribute("Include")?.Value ?? string.Empty))
        {
            if (disallowedProjectFragments.Any(fragment => include.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO005",
                    $"Project boundary violation: '{relativePath}' must not reference '{include}'.",
                    relativePath));
            }
        }

        foreach (var include in document.Descendants("PackageReference").Select(element => element.Attribute("Include")?.Value ?? string.Empty))
        {
            if (disallowedPackagePrefixes.Any(prefix => prefix.Length == 0 || include.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO006",
                    $"Project boundary violation: '{relativePath}' must not reference package '{include}'.",
                    relativePath));
            }
        }
    }

    private static async Task RequireExecutableNotPackableAsync(
        string repositoryRoot,
        string relativePath,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var document = await LoadXmlAsync(repositoryRoot, relativePath, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return;
        }

        var outputType = GetPropertyValue(document, "OutputType");
        if (!string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(GetPropertyValue(document, "IsPackable"), "false", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO007",
                $"Executable project '{relativePath}' must set IsPackable=false.",
                relativePath));
        }
    }

    private static async Task RequireTextTokensAsync(
        string repositoryRoot,
        string relativePath,
        IReadOnlyList<string> requiredTokens,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        foreach (var token in requiredTokens)
        {
            if (!text.Contains(token, StringComparison.Ordinal))
            {
                diagnostics.Add(new ProjectDoctorDiagnostic(
                    "REPO008",
                    $"Repository file '{relativePath}' must contain '{token}'.",
                    relativePath));
            }
        }
    }

    private static async Task<XDocument?> LoadXmlAsync(
        string repositoryRoot,
        string relativePath,
        ICollection<ProjectDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return XDocument.Parse(text, LoadOptions.SetLineInfo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO003",
                $"Repository XML file '{relativePath}' could not be loaded: {ex.Message}",
                relativePath));
            return null;
        }
    }

    private static void RequireProperty(
        XContainer document,
        string name,
        string expectedValue,
        string relativePath,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        if (!string.Equals(GetPropertyValue(document, name), expectedValue, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO003",
                $"Directory.Build.props must set {name}={expectedValue}.",
                relativePath));
        }
    }

    private static void RequireNonEmptyProperty(
        XContainer document,
        string name,
        string relativePath,
        ICollection<ProjectDoctorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(GetPropertyValue(document, name)))
        {
            diagnostics.Add(new ProjectDoctorDiagnostic(
                "REPO003",
                $"Directory.Build.props must set a non-empty {name}.",
                relativePath));
        }
    }

    private static string? GetPropertyValue(XContainer document, string name)
    {
        return document
            .Descendants(name)
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);
    }
}
