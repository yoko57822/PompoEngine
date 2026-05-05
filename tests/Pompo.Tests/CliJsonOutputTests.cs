using System.Text.Json;
using System.Security.Cryptography;
using Pompo.Build;
using Pompo.Cli;
using Pompo.Core;
using Pompo.Core.Project;
using Pompo.Core.Runtime;

namespace Pompo.Tests;

public sealed class CliJsonOutputTests
{
    private static readonly SemaphoreSlim ConsoleLock = new(1, 1);

    [Fact]
    public async Task VersionJson_EmitsEnvironmentMetadata()
    {
        var result = await RunCliAsync("version", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("PompoEngine", document.RootElement.GetProperty("product").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("cliVersion").GetString()));
        Assert.True(document.RootElement.GetProperty("schemaVersion").GetInt32() >= 1);
        Assert.Contains(".NET", document.RootElement.GetProperty("runtime").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("os").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("architecture").GetString()));
    }

    [Fact]
    public async Task InitJson_EmitsCreatedProjectMetadata()
    {
        var root = CreateTempDirectory();

        var result = await RunCliAsync("init", "--path", root, "--name", "Cli Init", "--template", "minimal", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal(root, document.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal("minimal", document.RootElement.GetProperty("template").GetString());
        Assert.Equal("Cli Init", document.RootElement.GetProperty("project").GetProperty("projectName").GetString());
        Assert.True(File.Exists(document.RootElement.GetProperty("projectFile").GetString()));
    }

    [Fact]
    public async Task ValidateJson_EmitsMachineReadableDiagnostics()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli Json");

        var result = await RunCliAsync("validate", "--project", root, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(root, document.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("projectDiagnostics").GetArrayLength());
        Assert.Equal(0, document.RootElement.GetProperty("graphDiagnostics").GetArrayLength());
        Assert.Equal(0, document.RootElement.GetProperty("assetDiagnostics").GetArrayLength());
    }

    [Fact]
    public async Task AssetListAndVerifyJson_EmitAssetAutomationResults()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Cli Assets");

        var listResult = await RunCliAsync("asset", "list", "--project", root, "--type", "Image", "--json");

        Assert.Equal(0, listResult.ExitCode);
        using var listDocument = JsonDocument.Parse(listResult.Stdout);
        Assert.Equal(root, listDocument.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal("Image", listDocument.RootElement.GetProperty("type").GetString());
        var assets = listDocument.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        Assert.Contains(assets, asset => asset.GetProperty("assetId").GetString() == "bg-intro");

        var verifyResult = await RunCliAsync("asset", "verify", "--project", root, "--json");

        Assert.Equal(0, verifyResult.ExitCode);
        using var verifyDocument = JsonDocument.Parse(verifyResult.Stdout);
        Assert.True(verifyDocument.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(verifyDocument.RootElement.GetProperty("assetCount").GetInt32() >= assets.Length);
        Assert.Equal(0, verifyDocument.RootElement.GetProperty("diagnostics").GetArrayLength());
    }

    [Fact]
    public async Task AssetMutationJson_EmitsImportRehashAndDeleteResults()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "source.txt");
        await File.WriteAllTextAsync(source, "asset-v1");
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli Asset Mutations");

        var importResult = await RunCliAsync(
            "asset",
            "import",
            "--project",
            root,
            "--file",
            source,
            "--type",
            "Data",
            "--asset-id",
            "script-data",
            "--json");

        Assert.Equal(0, importResult.ExitCode);
        using var importDocument = JsonDocument.Parse(importResult.Stdout);
        Assert.Equal(root, importDocument.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal("script-data", importDocument.RootElement.GetProperty("asset").GetProperty("assetId").GetString());

        await File.WriteAllTextAsync(Path.Combine(root, "Assets/Data/script-data.txt"), "asset-v2");
        var rehashResult = await RunCliAsync("asset", "rehash", "--project", root, "--json");

        Assert.Equal(0, rehashResult.ExitCode);
        using var rehashDocument = JsonDocument.Parse(rehashResult.Stdout);
        Assert.Equal(1, rehashDocument.RootElement.GetProperty("refreshed").GetInt32());

        var deleteResult = await RunCliAsync("asset", "delete", "--project", root, "--asset-id", "script-data", "--json");

        Assert.Equal(0, deleteResult.ExitCode);
        using var deleteDocument = JsonDocument.Parse(deleteResult.Stdout);
        Assert.Equal("script-data", deleteDocument.RootElement.GetProperty("assetId").GetString());
        Assert.True(deleteDocument.RootElement.GetProperty("fileDeleted").GetBoolean());
        Assert.False(File.Exists(Path.Combine(root, "Assets/Data/script-data.txt")));
    }

    [Fact]
    public async Task ProfileListJson_EmitsProfilesArray()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli Profiles");

        var result = await RunCliAsync("profile", "list", "--project", root, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal(root, document.RootElement.GetProperty("projectRoot").GetString());
        var profiles = document.RootElement.GetProperty("profiles").EnumerateArray().ToArray();
        Assert.Contains(profiles, profile => profile.GetProperty("profileName").GetString() == "debug");
        Assert.Contains(profiles, profile => profile.GetProperty("profileName").GetString() == "release");
    }

    [Fact]
    public async Task ProfileSaveAndDeleteJson_EmitProfileMutationResults()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli Profile Mutations");

        var saveResult = await RunCliAsync(
            "profile",
            "save",
            "--project",
            root,
            "--name",
            "demo",
            "--platform",
            "MacOS",
            "--app-name",
            "Demo VN",
            "--version",
            "0.2.0",
            "--data-only",
            "--json");

        Assert.Equal(0, saveResult.ExitCode);
        using var saveDocument = JsonDocument.Parse(saveResult.Stdout);
        Assert.Equal(root, saveDocument.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal("demo", saveDocument.RootElement.GetProperty("profile").GetProperty("profileName").GetString());
        Assert.False(saveDocument.RootElement.GetProperty("profile").GetProperty("packageRuntime").GetBoolean());

        var deleteResult = await RunCliAsync("profile", "delete", "--project", root, "--name", "demo", "--json");

        Assert.Equal(0, deleteResult.ExitCode);
        using var deleteDocument = JsonDocument.Parse(deleteResult.Stdout);
        Assert.Equal(root, deleteDocument.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal("demo", deleteDocument.RootElement.GetProperty("profileName").GetString());
    }

    [Fact]
    public async Task HistoryListJson_EmitsRecordedBuildEntries()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli History");
        await new BuildHistoryService().RecordAsync(
            root,
            new BuildHistoryEntry(
                DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
                "debug",
                PompoTargetPlatform.MacOS,
                Path.Combine(root, "Builds", "MacOS", "debug"),
                true,
                0,
                "Cli History",
                "0.1.0"));

        var result = await RunCliAsync("history", "list", "--project", root, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("debug", entry.GetProperty("profileName").GetString());
        Assert.True(entry.GetProperty("success").GetBoolean());
        Assert.Equal("Cli History", entry.GetProperty("appName").GetString());

        var clearResult = await RunCliAsync("history", "clear", "--project", root, "--json");

        Assert.Equal(0, clearResult.ExitCode);
        using var clearDocument = JsonDocument.Parse(clearResult.Stdout);
        Assert.Equal(root, clearDocument.RootElement.GetProperty("projectRoot").GetString());
        Assert.True(clearDocument.RootElement.GetProperty("cleared").GetBoolean());
    }

    [Fact]
    public async Task SaveListAndDeleteJson_EmitSaveSlotAutomationResults()
    {
        var root = CreateTempDirectory();
        await new RuntimeSaveStore().SaveAsync(root, "manual_1", "Manual 1", CreateSaveData("graph_intro", "node_1"));

        var listResult = await RunCliAsync("save", "list", "--saves", root, "--json");

        Assert.Equal(0, listResult.ExitCode);
        using var listDocument = JsonDocument.Parse(listResult.Stdout);
        Assert.Equal(root, listDocument.RootElement.GetProperty("saveRoot").GetString());
        var slot = Assert.Single(listDocument.RootElement.GetProperty("slots").EnumerateArray());
        Assert.Equal("manual_1", slot.GetProperty("slotId").GetString());
        Assert.Equal("graph_intro", slot.GetProperty("graphId").GetString());

        var deleteResult = await RunCliAsync("save", "delete", "--saves", root, "--slot", "manual_1", "--json");

        Assert.Equal(0, deleteResult.ExitCode);
        using var deleteDocument = JsonDocument.Parse(deleteResult.Stdout);
        Assert.Equal(root, deleteDocument.RootElement.GetProperty("saveRoot").GetString());
        Assert.Equal("manual_1", deleteDocument.RootElement.GetProperty("slot").GetString());
        Assert.False(File.Exists(RuntimeSaveStore.GetSlotPath(root, "manual_1")));
    }

    [Fact]
    public async Task LocalizationReportJson_EmitsCoverageSummary()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli Localization");

        var result = await RunCliAsync("localization", "report", "--project", root, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(root, document.RootElement.GetProperty("projectRoot").GetString());

        var report = document.RootElement.GetProperty("report");
        Assert.True(report.GetProperty("supportedLocaleCount").GetInt32() >= 1);
        Assert.True(report.GetProperty("stringTableCount").GetInt32() >= 1);
        Assert.True(report.GetProperty("entryCount").GetInt32() >= 1);
        Assert.Equal(0, report.GetProperty("missingValueCount").GetInt32());
        Assert.Equal(0, report.GetProperty("unsupportedValueCount").GetInt32());
    }

    [Fact]
    public async Task LocalizationMutationJson_EmitsChangedLocaleState()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Cli Locale Mutations");

        var addResult = await RunCliAsync("localization", "add-locale", "--project", root, "--locale", "ja", "--json");

        Assert.Equal(0, addResult.ExitCode);
        using var addDocument = JsonDocument.Parse(addResult.Stdout);
        Assert.Equal("ja", addDocument.RootElement.GetProperty("locale").GetString());
        Assert.Contains(
            addDocument.RootElement.GetProperty("locales").EnumerateArray(),
            locale => locale.GetString() == "ja");

        var repairResult = await RunCliAsync("localization", "repair", "--project", root, "--fallback-locale", "ko", "--json");

        Assert.Equal(0, repairResult.ExitCode);
        using var repairDocument = JsonDocument.Parse(repairResult.Stdout);
        Assert.True(repairDocument.RootElement.GetProperty("result").GetProperty("filledValueCount").GetInt32() > 0);
        Assert.Equal(0, repairDocument.RootElement.GetProperty("report").GetProperty("missingValueCount").GetInt32());

        var deleteResult = await RunCliAsync("localization", "delete-locale", "--project", root, "--locale", "ja", "--json");

        Assert.Equal(0, deleteResult.ExitCode);
        using var deleteDocument = JsonDocument.Parse(deleteResult.Stdout);
        Assert.Equal("ja", deleteDocument.RootElement.GetProperty("locale").GetString());
        Assert.DoesNotContain(
            deleteDocument.RootElement.GetProperty("locales").EnumerateArray(),
            locale => locale.GetString() == "ja");
    }

    [Fact]
    public async Task BuildVerifyJson_EmitsBuildOutputVerificationResult()
    {
        var root = CreateTempDirectory();
        await WriteMinimalBuildOutputAsync(root, "Cli Verify");

        var result = await RunCliAsync("build", "verify", "--build", root, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(root, document.RootElement.GetProperty("buildOutput").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("diagnostics").GetArrayLength());
    }

    [Fact]
    public async Task ReleasePackageAndVerifyJson_EmitReleaseAutomationResults()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteMinimalBuildOutputAsync(buildOutput, "Cli Release");

        var packageResult = await RunCliAsync(
            "release",
            "package",
            "--build",
            buildOutput,
            "--output",
            releaseOutput,
            "--name",
            "cli-release",
            "--json");

        Assert.Equal(0, packageResult.ExitCode);
        using var packageDocument = JsonDocument.Parse(packageResult.Stdout);
        var manifestPath = Path.Combine(releaseOutput, "cli-release.release.json");
        Assert.Equal(buildOutput, packageDocument.RootElement.GetProperty("buildOutput").GetString());
        Assert.Equal("cli-release", packageDocument.RootElement.GetProperty("manifest").GetProperty("packageName").GetString());
        Assert.True(File.Exists(manifestPath));

        var verifyResult = await RunCliAsync(
            "release",
            "verify",
            "--manifest",
            manifestPath,
            "--json");

        Assert.Equal(0, verifyResult.ExitCode);
        using var verifyDocument = JsonDocument.Parse(verifyResult.Stdout);
        Assert.True(verifyDocument.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(0, verifyDocument.RootElement.GetProperty("diagnostics").GetArrayLength());
        Assert.Equal("cli-release", verifyDocument.RootElement.GetProperty("manifest").GetProperty("packageName").GetString());
    }

    [Fact]
    public async Task ReleaseSignAndVerifySignatureJson_EmitSignatureAutomationResults()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteMinimalBuildOutputAsync(buildOutput, "Cli Signature");
        using var rsa = RSA.Create(2048);
        var privateKeyPath = Path.Combine(releaseOutput, "release-private.pem");
        var publicKeyPath = Path.Combine(releaseOutput, "release-public.pem");
        await File.WriteAllTextAsync(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
        await File.WriteAllTextAsync(publicKeyPath, rsa.ExportRSAPublicKeyPem());
        await new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "cli-signed");
        var manifestPath = Path.Combine(releaseOutput, "cli-signed.release.json");

        var signResult = await RunCliAsync(
            "release",
            "sign",
            "--manifest",
            manifestPath,
            "--private-key",
            privateKeyPath,
            "--json");

        Assert.Equal(0, signResult.ExitCode);
        using var signDocument = JsonDocument.Parse(signResult.Stdout);
        var signaturePath = signDocument.RootElement.GetProperty("signature").GetProperty("signaturePath").GetString();
        Assert.Equal("RSA-SHA256-PKCS1", signDocument.RootElement.GetProperty("signature").GetProperty("algorithm").GetString());
        Assert.True(File.Exists(signaturePath));

        var verifyResult = await RunCliAsync(
            "release",
            "verify-signature",
            "--manifest",
            manifestPath,
            "--public-key",
            publicKeyPath,
            "--json");

        Assert.Equal(0, verifyResult.ExitCode);
        using var verifyDocument = JsonDocument.Parse(verifyResult.Stdout);
        Assert.True(verifyDocument.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(0, verifyDocument.RootElement.GetProperty("diagnostics").GetArrayLength());
    }

    [Fact]
    public async Task ReleaseAuditJson_EmitsReleaseReadinessGates()
    {
        var root = CreateTempDirectory();

        var result = await RunCliAsync("release", "audit", "--root", root, "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal(root, document.RootElement.GetProperty("repositoryRoot").GetString());
        Assert.False(document.RootElement.GetProperty("ready").GetBoolean());
        Assert.True(document.RootElement.GetProperty("gates").GetArrayLength() > 0);
    }

    [Fact]
    public async Task DocsSiteJson_EmitsGeneratedDocumentationSiteManifest()
    {
        var root = CreateTempDirectory();
        var output = Path.Combine(root, "site");
        await DocumentationSiteServiceTests.WriteDocumentationSourcesAsync(root);

        var result = await RunCliAsync("docs", "site", "--root", root, "--output", output, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal(root, document.RootElement.GetProperty("repositoryRoot").GetString());
        Assert.Equal(output, document.RootElement.GetProperty("outputDirectory").GetString());
        Assert.True(File.Exists(Path.Combine(output, "index.html")));
        Assert.Equal(18, document.RootElement.GetProperty("manifest").GetProperty("pages").GetArrayLength());
    }

    [Fact]
    public async Task MissingReleaseCommand_ListsAllReleaseSubcommands()
    {
        var result = await RunCliAsync("release");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("package, verify, audit, sign, or verify-signature", result.Stderr, StringComparison.Ordinal);
    }

    private static async Task<CliResult> RunCliAsync(params string[] args)
    {
        await ConsoleLock.WaitAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await Program.Main(args);
            return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            ConsoleLock.Release();
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteMinimalBuildOutputAsync(string buildOutput, string appName)
    {
        var dataRoot = Path.Combine(buildOutput, "Data");
        Directory.CreateDirectory(dataRoot);
        await File.WriteAllTextAsync(Path.Combine(dataRoot, ProjectConstants.ProjectFileName), "{}");
        await File.WriteAllTextAsync(Path.Combine(dataRoot, "graph.pompo-ir.json"), "{}");
        var manifest = new BuildArtifactManifest(
            appName,
            "0.1.0",
            PompoTargetPlatform.MacOS,
            false,
            ["Data/project.pompo.json", "Data/graph.pompo-ir.json"],
            ["graph.pompo-ir.json"],
            ["ko"],
            []);
        await using var stream = File.Create(Path.Combine(buildOutput, "pompo-build-manifest.json"));
        await JsonSerializer.SerializeAsync(stream, manifest, ProjectFileService.CreateJsonOptions());
    }

    private static RuntimeSaveData CreateSaveData(string graphId, string nodeId)
    {
        return new RuntimeSaveData(
            ProjectConstants.CurrentSchemaVersion,
            graphId,
            nodeId,
            [],
            new Dictionary<string, object?> { ["score"] = 42 },
            "bg",
            [new RuntimeCharacterState("hero", "smile", RuntimeLayer.Character, 0.5f, 0.9f, true)],
            new RuntimeAudioState("bgm", ["sfx"]),
            ["choice-a"]);
    }

    private sealed record CliResult(int ExitCode, string Stdout, string Stderr);
}
