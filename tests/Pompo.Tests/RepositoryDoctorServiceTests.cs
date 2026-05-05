using System.Text.Json;
using Pompo.Build;
using Pompo.Core;
using Pompo.Core.Project;

namespace Pompo.Tests;

public sealed class RepositoryDoctorServiceTests
{
    [Fact]
    public async Task InspectAsync_AcceptsCurrentRepository()
    {
        var root = FindRepositoryRoot();

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.True(result.IsHealthy, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [Fact]
    public async Task InspectAsync_ReportsMissingRequiredRepositoryFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "REPO001" && diagnostic.Path == "LICENSE");
    }

    [Fact]
    public async Task InspectAsync_ReportsRuntimeBoundaryViolations()
    {
        var root = await CreateRepositorySkeletonAsync();
        await File.WriteAllTextAsync(
            Path.Combine(root, "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Pompo.Editor.Avalonia\Pompo.Editor.Avalonia.csproj" />
                <PackageReference Include="Avalonia" Version="12.0.2" />
              </ItemGroup>
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """);

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "REPO005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "REPO006");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "REPO007");
    }

    [Fact]
    public async Task InspectAsync_RequiresLocalizationReportJsonWorkflowGates()
    {
        var root = await CreateRepositorySkeletonAsync();
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/ci.yml"),
            "dotnet restore PompoEngine.slnx dotnet build PompoEngine.slnx --no-restore dotnet test PompoEngine.slnx --no-build docs site --root . --output artifacts/docs-site --json doctor --repository --root . init --path \"$project_dir\" --name SmokeVN --template sample --json asset import --project \"$project_dir\" --file \"$asset_file\" --type Image --asset-id smoke-bg --json asset verify --project \"$project_dir\" --json localization report --project \"$project_dir\" doctor --project build verify release verify --require-smoke-tested-locales --require-self-contained");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/release.yml"),
            "tags: Windows MacOS Linux doctor --repository --root . init --path $projectDir --name PompoEngineSample --template sample --json localization report --project $projectDir build verify release package release verify release sign verify-signature draft: true");

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "REPO008" &&
                diagnostic.Path == ".github/workflows/ci.yml" &&
                diagnostic.Message.Contains("localization report --project \"$project_dir\" --json", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "REPO008" &&
                diagnostic.Path == ".github/workflows/release.yml" &&
                diagnostic.Message.Contains("localization report --project $projectDir --json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InspectAsync_RequiresGeneratedOutputIgnorePatterns()
    {
        var root = await CreateRepositorySkeletonAsync();
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "bin/");
        await File.WriteAllTextAsync(Path.Combine(root, ".ignore"), "bin/");

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "REPO009" &&
                diagnostic.Path == ".gitignore" &&
                diagnostic.Message.Contains("obj/", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "REPO009" &&
                diagnostic.Path == ".ignore" &&
                diagnostic.Message.Contains("artifacts/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InspectAsync_RejectsGeneratedOsMetadataFiles()
    {
        var root = await CreateRepositorySkeletonAsync();
        await File.WriteAllTextAsync(Path.Combine(root, ".DS_Store"), "metadata");

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "REPO012" &&
                diagnostic.Path == ".DS_Store");
    }

    [Fact]
    public async Task InspectAsync_RequiresReleaseGateShellScriptExecutableOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = await CreateRepositorySkeletonAsync();
        var scriptPath = Path.Combine(root, "scripts/check-release-gates.sh");
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var result = await new RepositoryDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "REPO011" &&
                diagnostic.Path == "scripts/check-release-gates.sh");
    }

    [Fact]
    public async Task ReleaseAuditService_RequiresDocsSiteAndReleaseManifest()
    {
        var root = await CreateRepositorySkeletonAsync();

        var result = await new ReleaseAuditService().InspectAsync(root);

        Assert.False(result.IsReady);
        Assert.Contains(result.Gates, gate => gate.Gate == "repository-doctor" && gate.Passed);
        Assert.Contains(result.Gates, gate => gate.Gate == "docs-site" && !gate.Passed);
        Assert.Contains(result.Gates, gate => gate.Gate == "release-manifest" && !gate.Passed);
    }

    [Fact]
    public async Task ReleaseAuditService_AcceptsRepositoryDocsSiteAndStrictReleaseManifest()
    {
        var root = await CreateRepositorySkeletonAsync();
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "docs-site"));
        await File.WriteAllTextAsync(Path.Combine(root, "artifacts", "docs-site", "index.html"), "<h1>Docs</h1>");
        await File.WriteAllTextAsync(Path.Combine(root, "artifacts", "docs-site", "pompo-docs-site.json"), "{}");
        var buildOutput = Path.Combine(root, "Builds", "MacOS", "release");
        var releaseOutput = Path.Combine(root, "Releases");
        await WriteBuildManifestAsync(buildOutput, selfContained: true);
        await new ReleasePackageService()
            .PackageAsync(buildOutput, releaseOutput, "audit-release");

        var result = await new ReleaseAuditService().InspectAsync(
            root,
            Path.Combine(releaseOutput, "audit-release.release.json"),
            new ReleaseVerificationOptions(
                RequireSmokeTestedLocales: true,
                RequireSelfContained: true));

        Assert.True(result.IsReady, string.Join(Environment.NewLine, result.Gates.Where(gate => !gate.Passed)));
        Assert.Contains(result.Gates, gate => gate.Gate == "repository-doctor" && gate.Passed);
        Assert.Contains(result.Gates, gate => gate.Gate == "docs-site" && gate.Passed);
        Assert.Contains(result.Gates, gate => gate.Gate == "release-manifest" && gate.Passed);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PompoEngine.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate PompoEngine repository root.");
    }

    private static async Task<string> CreateRepositorySkeletonAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".github/workflows"));
        Directory.CreateDirectory(Path.Combine(root, ".github/ISSUE_TEMPLATE"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        foreach (var projectPath in new[]
        {
            "src/Pompo.Build/Pompo.Build.csproj",
            "src/Pompo.Cli/Pompo.Cli.csproj",
            "src/Pompo.Core/Pompo.Core.csproj",
            "src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj",
            "src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj",
            "src/Pompo.Scripting/Pompo.Scripting.csproj",
            "src/Pompo.VisualScripting/Pompo.VisualScripting.csproj",
            "tests/Pompo.Tests/Pompo.Tests.csproj"
        })
        {
            Directory.CreateDirectory(Path.Combine(root, Path.GetDirectoryName(projectPath)!));
            await File.WriteAllTextAsync(
                Path.Combine(root, projectPath),
                projectPath.Contains("Pompo.Cli", StringComparison.Ordinal) ||
                projectPath.Contains("Pompo.Editor.Avalonia", StringComparison.Ordinal) ||
                projectPath.Contains("Pompo.Runtime.Fna", StringComparison.Ordinal)
                    ? """
                      <Project Sdk="Microsoft.NET.Sdk">
                        <PropertyGroup>
                          <OutputType>Exe</OutputType>
                          <IsPackable>false</IsPackable>
                        </PropertyGroup>
                      </Project>
                      """
                    : "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        }

        await File.WriteAllTextAsync(
            Path.Combine(root, "global.json"),
            """
            {
              "sdk": {
                "version": "10.0.100",
                "rollForward": "disable"
              }
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <Deterministic>true</Deterministic>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <RepositoryType>git</RepositoryType>
                <VersionPrefix>0.1.0</VersionPrefix>
                <Authors>PompoEngine contributors</Authors>
                <Description>PompoEngine test repository.</Description>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "PompoEngine.slnx"),
            """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/Pompo.Build/Pompo.Build.csproj" />
                <Project Path="src/Pompo.Cli/Pompo.Cli.csproj" />
                <Project Path="src/Pompo.Core/Pompo.Core.csproj" />
                <Project Path="src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj" />
                <Project Path="src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj" />
                <Project Path="src/Pompo.Scripting/Pompo.Scripting.csproj" />
                <Project Path="src/Pompo.VisualScripting/Pompo.VisualScripting.csproj" />
              </Folder>
              <Folder Name="/tests/">
                <Project Path="tests/Pompo.Tests/Pompo.Tests.csproj" />
              </Folder>
            </Solution>
            """);

        await File.WriteAllTextAsync(Path.Combine(root, ".editorconfig"), "root = true");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".gitattributes"),
            "* text=auto eol=lf *.cs text eol=lf *.json text eol=lf *.md text eol=lf *.sh text eol=lf *.ps1 text eol=lf *.png binary *.wav binary *.zip binary");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".gitignore"),
            "bin/ obj/ TestResults/ .DS_Store Thumbs.db Desktop.ini artifacts/ dist/");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".ignore"),
            "bin/ obj/ TestResults/ .DS_Store Thumbs.db Desktop.ini artifacts/ dist/");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/dependabot.yml"),
            "version: 2 package-ecosystem: nuget package-ecosystem: github-actions interval: weekly");
        await File.WriteAllTextAsync(Path.Combine(root, "LICENSE"), "MIT");
        await File.WriteAllTextAsync(Path.Combine(root, "CHANGELOG.md"), "# Changelog");
        await File.WriteAllTextAsync(Path.Combine(root, "MAINTAINERS.md"), "# Maintainers");
        await File.WriteAllTextAsync(Path.Combine(root, "CONTRIBUTING.md"), "# Contributing");
        await File.WriteAllTextAsync(Path.Combine(root, "CODE_OF_CONDUCT.md"), "# Code of Conduct");
        await File.WriteAllTextAsync(Path.Combine(root, "SUPPORT.md"), "# Support");
        await File.WriteAllTextAsync(Path.Combine(root, "SECURITY.md"), "# Security");
        await File.WriteAllTextAsync(
            Path.Combine(root, "README.md"),
            "LICENSE CHANGELOG.md MAINTAINERS.md CONTRIBUTING.md CODE_OF_CONDUCT.md SUPPORT.md SECURITY.md docs/GETTING_STARTED.md docs/RUN_AND_USE.md docs/DEVELOPMENT.md docs/TROUBLESHOOTING.md docs/ARCHITECTURE.md docs/OPEN_SOURCE_RELEASE_CHECKLIST.md docs/PRODUCTION_AUDIT.md docs/ROADMAP.md docs/COMPATIBILITY.md docs/SCRIPTING.md docs/RELEASING.md CHANGELOG.md MAINTAINERS.md scripts/check-release-gates.sh scripts/check-release-gates.ps1 docs site not 1.0 yet");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/ci.yml"),
            "permissions: contents: read dotnet restore PompoEngine.slnx dotnet build PompoEngine.slnx --no-restore dotnet test PompoEngine.slnx --no-build docs site --root . --output artifacts/docs-site --json doctor --repository --root . init --path \"$project_dir\" --name SmokeVN --template sample --json asset import --project \"$project_dir\" --file \"$asset_file\" --type Image --asset-id smoke-bg --json asset verify --project \"$project_dir\" --json localization report --project \"$project_dir\" --json doctor --project build verify release verify --require-smoke-tested-locales --require-self-contained");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/release.yml"),
            "permissions: contents: read contents: write tags: Windows MacOS Linux doctor --repository --root . init --path $projectDir --name PompoEngineSample --template sample --json localization report --project $projectDir --json build verify release package release verify release sign verify-signature draft: true");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/docs.yml"),
            "docs site --root . --output artifacts/docs-site --json actions/configure-pages actions/upload-pages-artifact actions/deploy-pages github-pages");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/dependency-review.yml"),
            "pull_request: permissions: contents: read pull-requests: read actions/dependency-review-action@v4 fail-on-severity: high");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".github/workflows/codeql.yml"),
            "github/codeql-action/init@v4 github/codeql-action/analyze@v4 languages: csharp security-events: write dotnet build PompoEngine.slnx --no-restore");
        await File.WriteAllTextAsync(Path.Combine(root, ".github/ISSUE_TEMPLATE/bug_report.md"), "bug");
        await File.WriteAllTextAsync(Path.Combine(root, ".github/ISSUE_TEMPLATE/feature_request.md"), "feature");
        await File.WriteAllTextAsync(Path.Combine(root, ".github/ISSUE_TEMPLATE/config.yml"), "blank_issues_enabled: false");
        await File.WriteAllTextAsync(Path.Combine(root, ".github/pull_request_template.md"), "pr");
        await File.WriteAllTextAsync(
            Path.Combine(root, "scripts/check-release-gates.sh"),
            "set -euo pipefail dotnet restore PompoEngine.slnx dotnet build PompoEngine.slnx --no-restore dotnet test PompoEngine.slnx --no-build version --json src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json docs site --root . --output artifacts/docs-site --json doctor --repository --root . --validate-runtime Release gates passed");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                Path.Combine(root, "scripts/check-release-gates.sh"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        await File.WriteAllTextAsync(
            Path.Combine(root, "scripts/check-release-gates.ps1"),
            "$ErrorActionPreference = \"Stop\" Set-StrictMode -Version Latest dotnet restore PompoEngine.slnx dotnet build PompoEngine.slnx --no-restore dotnet test PompoEngine.slnx --no-build version --json src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json docs site --root . --output artifacts/docs-site --json doctor --repository --root . --validate-runtime Release gates passed");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs/OPEN_SOURCE_RELEASE_CHECKLIST.md"),
            "dotnet build PompoEngine.slnx --no-restore dotnet test PompoEngine.slnx --no-build scripts/check-release-gates.sh scripts/check-release-gates.ps1 docs site --root . --output artifacts/docs-site --json Release Gates docs/PRODUCTION_AUDIT.md Packaging Gates");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs/PRODUCTION_AUDIT.md"),
            "Prompt-To-Artifact Checklist Required Gates scripts/check-release-gates.sh scripts/check-release-gates.ps1 Completion Assessment remaining production risks");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/GETTING_STARTED.md"), "getting started");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/RUN_AND_USE.md"), "run and use");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/DEVELOPMENT.md"), "scripts/check-release-gates.sh scripts/check-release-gates.ps1 Project Boundaries Pompo.Core Pompo.Runtime.Fna Pompo.VisualScripting DocumentationSiteService RepositoryDoctorService .DS_Store");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/TROUBLESHOOTING.md"), "Project Does Not Open Graph Preview Fails User Scripts Fail to Compile Build Fails Release Verification Fails Documentation Site Fails");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/ARCHITECTURE.md"), "Project Boundaries Authoring Data Flow Build Flow Release Flow Runtime Flow Architectural Rules");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/ROADMAP.md"), "roadmap");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/COMPATIBILITY.md"), "schemaVersion CHANGELOG.md PompoGraphIR release manifest scripting API JSON output");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/SCRIPTING.md"), "PompoCommandNode PompoConditionNode PompoRuntimeContext PompoNodeInput scriptPermissions Pompo.UserScripts.dll");
        await File.WriteAllTextAsync(Path.Combine(root, "docs/RELEASING.md"), "releasing");
        return root;
    }

    private static async Task WriteBuildManifestAsync(
        string buildOutput,
        bool selfContained = false)
    {
        var dataDirectory = Path.Combine(buildOutput, "Data");
        var runtimeDirectory = Path.Combine(buildOutput, "Runtime");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(runtimeDirectory);
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, ProjectConstants.ProjectFileName), "{}");
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "graph.pompo-ir.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(runtimeDirectory, "Pompo.Runtime.Fna"), "#!/usr/bin/env dotnet");

        var manifest = new BuildArtifactManifest(
            "Sample",
            "0.1.0",
            PompoTargetPlatform.MacOS,
            selfContained,
            ["Data/project.pompo.json", "Data/graph.pompo-ir.json", "Runtime/Pompo.Runtime.Fna"],
            ["graph.pompo-ir.json"],
            ["ko", "en"],
            ["ko", "en"]);

        await using var stream = File.Create(Path.Combine(buildOutput, "pompo-build-manifest.json"));
        await JsonSerializer.SerializeAsync(stream, manifest, ProjectFileService.CreateJsonOptions());
    }
}
