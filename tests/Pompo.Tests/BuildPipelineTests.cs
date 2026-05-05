using System.Text.Json.Nodes;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using Pompo.Build;
using Pompo.Core;
using Pompo.Core.Assets;
using Pompo.Core.Graphs;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.VisualScripting;

namespace Pompo.Tests;

public sealed class BuildPipelineTests
{
    [Fact]
    public async Task BuildAsync_WritesManifestWithoutEditorAssemblies()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var projectFiles = new ProjectFileService();
        var project = new PompoProjectDocument
        {
            ProjectName = "Sample",
            Graphs = [GraphFixtures.LinearGraph()]
        };
        await projectFiles.SaveAsync(root, project);

        var pipeline = new PompoBuildPipeline();
        var result = await pipeline.BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0", PackageRuntime: false),
            output);

        Assert.True(result.Success);
        Assert.NotNull(result.Manifest);
        Assert.DoesNotContain(result.Manifest.IncludedFiles, file => file.Contains("Editor", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.Manifest.SelfContained);
        Assert.Equal(["ko", "en"], result.Manifest.SupportedLocales);
        Assert.Empty(result.Manifest.SmokeTestedLocales);
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "pompo-build-manifest.json")));
        Assert.False(Directory.Exists(Path.Combine(result.OutputDirectory, "Runtime")));

        await using var manifestStream = File.OpenRead(Path.Combine(result.OutputDirectory, "pompo-build-manifest.json"));
        var manifest = await JsonSerializer.DeserializeAsync<BuildArtifactManifest>(
            manifestStream,
            ProjectFileService.CreateJsonOptions());
        Assert.NotNull(manifest);
        Assert.False(manifest.SelfContained);
        Assert.Equal(["ko", "en"], manifest.SupportedLocales);
        Assert.Empty(manifest.SmokeTestedLocales);
    }

    [Fact]
    public async Task BuildAsync_FailsWhenRegisteredAssetSourceIsMissing()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var projectFiles = new ProjectFileService();
        var project = new PompoProjectDocument
        {
            ProjectName = "Sample",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "missing-bg",
                        "Assets/Images/missing.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "missing-hash",
                        [])
                ]
            },
            Graphs = [GraphFixtures.LinearGraph()]
        };
        await projectFiles.SaveAsync(root, project);

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0"),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BUILD002");
    }

    [Fact]
    public async Task BuildAsync_RejectsUnsafeAssetSourcePathBeforeCopying()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var projectFiles = new ProjectFileService();
        var project = new PompoProjectDocument
        {
            ProjectName = "Unsafe Asset Path",
            Assets = new AssetDatabase
            {
                Assets =
                [
                    new AssetMetadata(
                        "outside",
                        "../outside.png",
                        PompoAssetType.Image,
                        new AssetImportOptions(),
                        "hash",
                        [])
                ]
            },
            Graphs = [GraphFixtures.LinearGraph()]
        };
        await projectFiles.SaveAsync(root, project);
        await File.WriteAllTextAsync(Path.Combine(Directory.GetParent(root)!.FullName, "outside.png"), "outside");

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code is "POMPO036" or "BUILD007");
        Assert.False(File.Exists(Path.Combine(output, "outside.png")));
    }

    [Fact]
    public async Task BuildAsync_FailsWhenGraphReferencesMissingAsset()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var project = new PompoProjectDocument
        {
            ProjectName = "Sample",
            Graphs =
            [
                new GraphDocument(
                    1,
                    "intro",
                    [
                        new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []),
                        new GraphNode(
                            "bg",
                            GraphNodeKind.ChangeBackground,
                            new GraphPoint(200, 0),
                            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
                            new JsonObject { ["assetId"] = "missing-bg" }),
                        new GraphNode("end", GraphNodeKind.EndScene, new GraphPoint(400, 0), [NodeCatalog.InExecPort()], [])
                    ],
                    [
                        new GraphEdge("e1", "start", "out", "bg", "in"),
                        new GraphEdge("e2", "bg", "out", "end", "in")
                    ])
            ]
        };
        await new ProjectFileService().SaveAsync(root, project);

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO011" && diagnostic.Path == "intro");
    }

    [Fact]
    public async Task BuildAsync_FailsWhenProjectHasNoGraphs()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Sample",
                Graphs = []
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO026");
    }

    [Fact]
    public async Task BuildAsync_RejectsUnsafeGraphIdBeforeWritingIr()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Unsafe Graph",
                Graphs = [new GraphDocument(1, "../outside", [], [])]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Unsafe Graph", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO037");
        Assert.False(File.Exists(Path.Combine(output, "MacOS", "debug", "outside.pompo-ir.json")));
    }

    [Fact]
    public async Task BuildAsync_FailsWhenProjectHasGraphCallCycle()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Cyclic",
                Graphs =
                [
                    new GraphDocument(
                        1,
                        "intro",
                        [new GraphNode("call_cafe", GraphNodeKind.CallGraph, new GraphPoint(0, 0), [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()], new JsonObject { ["graphId"] = "cafe" })],
                        []),
                    new GraphDocument(
                        1,
                        "cafe",
                        [new GraphNode("call_intro", GraphNodeKind.CallGraph, new GraphPoint(0, 0), [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()], new JsonObject { ["graphId"] = "intro" })],
                        [])
                ]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO027" && diagnostic.Path == "cafe");
    }

    [Fact]
    public async Task BuildAsync_FailsWhenRuntimePackagingIsRequestedButRuntimeProjectIsMissing()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Sample",
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile(
                "debug",
                PompoTargetPlatform.MacOS,
                "Sample",
                "0.1.0",
                RuntimeProjectPath: Path.Combine(root, "missing-runtime.csproj")),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BUILD003");
    }

    [Fact]
    public async Task BuildAsync_FailsWhenSmokeTestIsRequestedWithoutRuntimePackage()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Sample",
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile(
                "debug",
                CurrentPlatform(),
                "Sample",
                "0.1.0",
                RunSmokeTest: true,
                PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BUILD005");
    }

    [Fact]
    public async Task PackagedRuntimeSmokeTester_RunsRuntimeExecutableAgainstCompiledGraphs()
    {
        var output = CreateTempDirectory();
        var dataDirectory = Path.Combine(output, "Data");
        Directory.CreateDirectory(dataDirectory);
        var ir = new GraphCompiler().Compile(LocalizedGraph());
        await using (var stream = File.Create(Path.Combine(dataDirectory, "intro.pompo-ir.json")))
        {
            await JsonSerializer.SerializeAsync(stream, ir, ProjectFileService.CreateJsonOptions());
        }

        await using (var stream = File.Create(Path.Combine(dataDirectory, ProjectConstants.ProjectFileName)))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new PompoProjectDocument
                {
                    ProjectName = "Smoke Locale",
                    StringTables =
                    [
                        new StringTableDocument(
                            "dialogue",
                            [
                                new StringTableEntry(
                                    "line.hello",
                                    new Dictionary<string, string>
                                    {
                                        ["ko"] = "안녕",
                                        ["en"] = "Hello"
                                    })
                            ])
                    ]
                },
                ProjectFileService.CreateJsonOptions());
        }

        var diagnostics = await new PackagedRuntimeSmokeTester().RunAsync(
            output,
            AppContext.BaseDirectory,
            ["intro.pompo-ir.json"],
            ["ko", "en"]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task PackagedRuntimeSmokeTester_LoadsSiblingIrFilesForCallGraph()
    {
        var output = CreateTempDirectory();
        var dataDirectory = Path.Combine(output, "Data");
        Directory.CreateDirectory(dataDirectory);
        var compiler = new GraphCompiler();
        await using (var rootStream = File.Create(Path.Combine(dataDirectory, "root.pompo-ir.json")))
        {
            await JsonSerializer.SerializeAsync(rootStream, compiler.Compile(CallGraphRoot()), ProjectFileService.CreateJsonOptions());
        }

        await using (var childStream = File.Create(Path.Combine(dataDirectory, "child.pompo-ir.json")))
        {
            await JsonSerializer.SerializeAsync(childStream, compiler.Compile(CallGraphChild()), ProjectFileService.CreateJsonOptions());
        }

        var diagnostics = await new PackagedRuntimeSmokeTester().RunAsync(
            output,
            AppContext.BaseDirectory,
            ["root.pompo-ir.json"]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task BuildAsync_CopiesAssetsWithoutDuplicatingAssetsFolder()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var assetPath = Path.Combine(root, "Assets", "Images", "bg.png");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "image");
        var hash = await AssetDatabaseService.ComputeSha256Async(assetPath);
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Sample",
                Assets = new AssetDatabase
                {
                    Assets =
                    [
                        new AssetMetadata(
                            "bg",
                            "Assets/Images/bg.png",
                            PompoAssetType.Image,
                            new AssetImportOptions(),
                            hash,
                            [])
                    ]
                },
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Sample", "0.1.0", PackageRuntime: false),
            output);

        Assert.True(result.Success);
        Assert.Contains("Assets/Images/bg.png", result.Manifest!.IncludedFiles);
        Assert.DoesNotContain("Assets/Assets/Images/bg.png", result.Manifest.IncludedFiles);
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "Assets", "Images", "bg.png")));
    }

    [Fact]
    public async Task BuildAsync_CompilesUserScriptsIntoBuildOutput()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "Scripts", "HelloNode.cs"),
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Pompo.Scripting;

            public sealed class HelloNode : PompoCommandNode
            {
                public override ValueTask ExecuteAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                {
                    context.SetVariable("hello", "world");
                    return ValueTask.CompletedTask;
                }
            }
            """);
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Scripted",
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Scripted", "0.1.0", PackageRuntime: false),
            output);

        Assert.True(result.Success);
        Assert.Contains("Scripts/Pompo.UserScripts.dll", result.Manifest!.IncludedFiles);
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "Scripts", "Pompo.UserScripts.dll")));
        Assert.DoesNotContain(result.Manifest.IncludedFiles, file => file.EndsWith("HelloNode.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildAsync_FailsWhenUserScriptRequiresBlockedPermission()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "Scripts", "BadNode.cs"),
            "using System.IO; public sealed class BadNode { public string Read() => File.ReadAllText(\"secret.txt\"); }");
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Blocked Script",
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Blocked Script", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BUILD011" && diagnostic.Path == "Scripts/BadNode.cs");
        Assert.False(File.Exists(Path.Combine(output, "MacOS", "debug", "Scripts", "Pompo.UserScripts.dll")));
    }

    [Fact]
    public async Task BuildAsync_AllowsUserScriptPermissionWhenProjectOptedIn()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "Scripts", "FileNode.cs"),
            "using System.IO; public sealed class FileNode { public bool Exists(string path) => File.Exists(path); }");
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Permitted Script",
                ScriptPermissions = new PompoScriptPermissions(AllowFileSystem: true),
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Permitted Script", "0.1.0", PackageRuntime: false),
            output);

        Assert.True(result.Success);
        Assert.Contains("Scripts/Pompo.UserScripts.dll", result.Manifest!.IncludedFiles);
        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "Scripts", "Pompo.UserScripts.dll")));
    }

    [Fact]
    public async Task BuildAsync_ReportsUserScriptCompileErrorPath()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "Scripts", "BrokenNode.cs"),
            "public sealed class BrokenNode {");
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Broken Script",
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile("debug", PompoTargetPlatform.MacOS, "Broken Script", "0.1.0", PackageRuntime: false),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BUILD011" && diagnostic.Path == "Scripts/BrokenNode.cs");
    }

    [Theory]
    [InlineData("../debug")]
    [InlineData(".debug")]
    [InlineData("debug.")]
    [InlineData("bad profile")]
    public async Task BuildAsync_RejectsUnsafeProfileNameBeforeCreatingOutput(string profileName)
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Unsafe Profile",
                Graphs = [GraphFixtures.LinearGraph()]
            });

        var result = await new PompoBuildPipeline().BuildAsync(
            root,
            new PompoBuildProfile(profileName, PompoTargetPlatform.MacOS, "Unsafe Profile", "0.1.0"),
            output);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BUILD006");
        Assert.False(Directory.Exists(Path.Combine(output, "MacOS", profileName)));
    }

    [Fact]
    public async Task BuildProfileFileService_RoundTripsProfileJson()
    {
        var root = CreateTempDirectory();
        var path = BuildProfileFileService.GetDefaultProfilePath(root, "release-mac");
        var profile = new PompoBuildProfile(
            "release-mac",
            PompoTargetPlatform.MacOS,
            "Sample VN",
            "1.2.3",
            RunSmokeTest: true,
            PackageRuntime: false,
            RuntimeProjectPath: "runtime.csproj",
            SelfContained: true);

        var files = new BuildProfileFileService();
        await files.SaveAsync(path, profile);
        var loaded = await files.LoadAsync(path);

        Assert.Equal(profile.ProfileName, loaded.ProfileName);
        Assert.Equal(profile.Platform, loaded.Platform);
        Assert.Equal(profile.AppName, loaded.AppName);
        Assert.Equal(profile.Version, loaded.Version);
        Assert.True(loaded.RunSmokeTest);
        Assert.False(loaded.PackageRuntime);
        Assert.Equal("runtime.csproj", loaded.RuntimeProjectPath);
        Assert.True(loaded.SelfContained);
    }

    [Fact]
    public async Task BuildProfileFileService_ListsAndDeletesProjectProfiles()
    {
        var root = CreateTempDirectory();
        var files = new BuildProfileFileService();
        await files.SaveProjectProfileAsync(
            root,
            new PompoBuildProfile(
                "debug",
                PompoTargetPlatform.MacOS,
                "Sample",
                "0.1.0",
                PackageRuntime: false));
        await files.SaveProjectProfileAsync(
            root,
            new PompoBuildProfile(
                "release",
                PompoTargetPlatform.Linux,
                "Sample",
                "1.0.0",
                RunSmokeTest: true,
                SelfContained: true));

        var profiles = await files.LoadProjectProfilesAsync(root);

        Assert.Equal(["debug", "release"], profiles.Select(profile => profile.ProfileName).ToArray());

        await files.DeleteProjectProfileAsync(root, "debug");

        var remaining = Assert.Single(await files.LoadProjectProfilesAsync(root));
        Assert.Equal("release", remaining.ProfileName);
        Assert.False(File.Exists(BuildProfileFileService.GetDefaultProfilePath(root, "debug")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => files.DeleteProjectProfileAsync(root, "release"));
    }

    [Fact]
    public async Task BuildProfileFileService_RejectsUnsafeProfileNames()
    {
        var root = CreateTempDirectory();
        var files = new BuildProfileFileService();

        Assert.Throws<ArgumentException>(() => BuildProfileFileService.GetDefaultProfilePath(root, "../release"));
        Assert.Throws<ArgumentException>(() => BuildProfileFileService.GetDefaultProfilePath(root, ".release"));
        Assert.Throws<ArgumentException>(() => BuildProfileFileService.GetDefaultProfilePath(root, "release."));
        await Assert.ThrowsAsync<ArgumentException>(() => files.SaveProjectProfileAsync(
            root,
            new PompoBuildProfile("../release", PompoTargetPlatform.MacOS, "Sample", "0.1.0")));
    }

    [Fact]
    public async Task ReleasePackageService_CreatesZipChecksumAndManifest()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(buildOutput, "Data"));
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"), "{}");
        await WriteBuildManifestAsync(buildOutput);

        var manifest = await new ReleasePackageService().PackageAsync(
            buildOutput,
            releaseOutput,
            "sample-0.1.0-macos");

        Assert.Equal("Sample", manifest.BuildAppName);
        Assert.Equal("0.1.0", manifest.BuildVersion);
        Assert.Equal(PompoTargetPlatform.MacOS, manifest.BuildPlatform);
        Assert.False(manifest.BuildSelfContained);
        Assert.Equal(["ko", "en"], manifest.SupportedLocales);
        Assert.Equal(["ko", "en"], manifest.SmokeTestedLocales);
        Assert.Equal(["Data/project.pompo.json", "Data/graph.pompo-ir.json"], manifest.IncludedFiles);
        Assert.Equal(["graph.pompo-ir.json"], manifest.CompiledGraphs);
        Assert.True(File.Exists(manifest.ArchivePath));
        Assert.True(File.Exists(manifest.Sha256Path));
        Assert.True(File.Exists(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json")));
        Assert.Equal(await ReleasePackageService.ComputeSha256Async(manifest.ArchivePath), manifest.Sha256);
        Assert.Contains("sample-0.1.0-macos.zip", await File.ReadAllTextAsync(manifest.Sha256Path));
        Assert.True(manifest.ArchiveSizeBytes > 0);

        var verified = await new ReleasePackageService().VerifyAsync(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json"));
        Assert.True(verified.IsValid);
        Assert.Empty(verified.Diagnostics);
        Assert.Equal(manifest.Sha256, verified.Manifest!.Sha256);
    }

    [Theory]
    [InlineData("../sample")]
    [InlineData("sample release")]
    [InlineData(".hidden")]
    [InlineData("sample.")]
    public async Task ReleasePackageService_RejectsUnsafePackageName(string packageName)
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, packageName));

        Assert.Contains("Release package name", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(releaseOutput));
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsPackageNameAndFileNameMismatches()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var tamperedManifest = manifest with
        {
            PackageName = "sample-0.1.0-linux",
            Sha256Path = Path.Combine(releaseOutput, "sample-0.1.0-macos.checksum")
        };
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, tamperedManifest, ProjectFileService.CreateJsonOptions());
        }

        var verified = await service.VerifyAsync(manifestPath);

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL034");
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL035");
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL036");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsTamperedArchive()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(buildOutput, "Data"));
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"), "{}");
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        await File.AppendAllTextAsync(manifest.ArchivePath, "tampered");

        var verified = await service.VerifyAsync(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json"));

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL007");
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL008");
    }

    [Fact]
    public async Task ReleasePackageService_SignsAndVerifiesArchiveSignature()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(buildOutput, "Data"));
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"), "{}");
        await WriteBuildManifestAsync(buildOutput);
        using var rsa = RSA.Create(2048);
        var privateKeyPath = Path.Combine(releaseOutput, "release-private.pem");
        var publicKeyPath = Path.Combine(releaseOutput, "release-public.pem");
        await File.WriteAllTextAsync(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
        await File.WriteAllTextAsync(publicKeyPath, rsa.ExportRSAPublicKeyPem());
        var service = new ReleasePackageService();
        await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");

        var signature = await service.SignAsync(manifestPath, privateKeyPath);
        var verified = await service.VerifySignatureAsync(manifestPath, publicKeyPath);

        Assert.True(File.Exists(signature.SignaturePath));
        Assert.Equal("RSA-SHA256-PKCS1", signature.Algorithm);
        Assert.True(verified.IsValid);
        Assert.Empty(verified.Diagnostics);
    }

    [Fact]
    public async Task ReleasePackageService_VerifySignatureReportsWrongKey()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(buildOutput, "Data"));
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"), "{}");
        await WriteBuildManifestAsync(buildOutput);
        using var signingKey = RSA.Create(2048);
        using var wrongKey = RSA.Create(2048);
        var privateKeyPath = Path.Combine(releaseOutput, "release-private.pem");
        var wrongPublicKeyPath = Path.Combine(releaseOutput, "wrong-public.pem");
        await File.WriteAllTextAsync(privateKeyPath, signingKey.ExportRSAPrivateKeyPem());
        await File.WriteAllTextAsync(wrongPublicKeyPath, wrongKey.ExportRSAPublicKeyPem());
        var service = new ReleasePackageService();
        await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");
        await service.SignAsync(manifestPath, privateKeyPath);

        var verified = await service.VerifySignatureAsync(manifestPath, wrongPublicKeyPath);

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL014");
    }

    [Fact]
    public async Task ReleasePackageService_RejectsInvalidBuildManifest()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(buildOutput, "Data"));
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "pompo-build-manifest.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"), "{}");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("not packageable", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsBuildManifestWithoutCompiledGraphs()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput, compiledGraphs: []);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("CompiledGraphs is empty", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsBuildManifestWithoutProjectData()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput, includeProjectData: false);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("IncludedFiles does not contain required project data 'Data/project.pompo.json'", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsBuildManifestWithMissingIncludedFile()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput, writeGraphFile: false);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("Included file 'Data/graph.pompo-ir.json' does not exist", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsForbiddenRuntimeArtifactsInBuildOutput()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var editorAssemblyPath = Path.Combine(buildOutput, "Runtime", "Pompo.Editor.Avalonia.dll");
        var buildAssemblyPath = Path.Combine(buildOutput, "Runtime", "Pompo.Build.dll");
        var compilerAssemblyPath = Path.Combine(buildOutput, "Runtime", "Microsoft.CodeAnalysis.CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(editorAssemblyPath)!);
        await File.WriteAllTextAsync(editorAssemblyPath, "editor");
        await File.WriteAllTextAsync(buildAssemblyPath, "build");
        await File.WriteAllTextAsync(compilerAssemblyPath, "compiler");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("forbidden runtime artifact 'Runtime/Pompo.Editor.Avalonia.dll'", ex.Message);
        Assert.Contains("forbidden runtime artifact 'Runtime/Pompo.Build.dll'", ex.Message);
        Assert.Contains("forbidden runtime artifact 'Runtime/Microsoft.CodeAnalysis.CSharp.dll'", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsUnmanifestedBuildOutputFile()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "stray.txt"), "stray");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("unmanifested file 'stray.txt'", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsSourceScriptArtifactsInBuildOutput()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var scriptPath = Path.Combine(buildOutput, "Scripts", "LeakedNode.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "public sealed class LeakedNode { }");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("source script artifact 'Scripts/LeakedNode.cs'", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_RejectsDebugSymbolArtifactsInBuildOutput()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var pdbPath = Path.Combine(buildOutput, "Runtime", "Pompo.Runtime.Fna.pdb");
        Directory.CreateDirectory(Path.GetDirectoryName(pdbPath)!);
        await File.WriteAllTextAsync(pdbPath, "symbols");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ReleasePackageService().PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos"));

        Assert.Contains("debug symbol artifact 'Runtime/Pompo.Runtime.Fna.pdb'", ex.Message);
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsArchiveMissingIncludedFile()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var replacementRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(replacementRoot, "Data"));
        File.Copy(
            Path.Combine(buildOutput, "pompo-build-manifest.json"),
            Path.Combine(replacementRoot, "pompo-build-manifest.json"));
        File.Copy(
            Path.Combine(buildOutput, "Data", ProjectConstants.ProjectFileName),
            Path.Combine(replacementRoot, "Data", ProjectConstants.ProjectFileName));
        File.Delete(manifest.ArchivePath);
        ZipFile.CreateFromDirectory(replacementRoot, manifest.ArchivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        var replacementHash = await ReleasePackageService.ComputeSha256Async(manifest.ArchivePath);
        var replacementManifest = manifest with
        {
            Sha256 = replacementHash,
            ArchiveSizeBytes = new FileInfo(manifest.ArchivePath).Length
        };
        await using (var stream = File.Create(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json")))
        {
            await JsonSerializer.SerializeAsync(stream, replacementManifest, ProjectFileService.CreateJsonOptions());
        }

        var verified = await service.VerifyAsync(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json"));

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL021");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsEditorArtifactsInArchive()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var replacementRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(replacementRoot, "Data"));
        Directory.CreateDirectory(Path.Combine(replacementRoot, "Runtime"));
        File.Copy(
            Path.Combine(buildOutput, "pompo-build-manifest.json"),
            Path.Combine(replacementRoot, "pompo-build-manifest.json"));
        File.Copy(
            Path.Combine(buildOutput, "Data", ProjectConstants.ProjectFileName),
            Path.Combine(replacementRoot, "Data", ProjectConstants.ProjectFileName));
        File.Copy(
            Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"),
            Path.Combine(replacementRoot, "Data", "graph.pompo-ir.json"));
        await File.WriteAllTextAsync(Path.Combine(replacementRoot, "Runtime", "Avalonia.Controls.dll"), "editor");
        File.Delete(manifest.ArchivePath);
        ZipFile.CreateFromDirectory(replacementRoot, manifest.ArchivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        var replacementHash = await ReleasePackageService.ComputeSha256Async(manifest.ArchivePath);
        var replacementManifest = manifest with
        {
            Sha256 = replacementHash,
            ArchiveSizeBytes = new FileInfo(manifest.ArchivePath).Length
        };
        await using (var stream = File.Create(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json")))
        {
            await JsonSerializer.SerializeAsync(stream, replacementManifest, ProjectFileService.CreateJsonOptions());
        }

        var verified = await service.VerifyAsync(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json"));

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL028");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsUnmanifestedArchiveFile()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var replacementRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(replacementRoot, "Data"));
        File.Copy(
            Path.Combine(buildOutput, "pompo-build-manifest.json"),
            Path.Combine(replacementRoot, "pompo-build-manifest.json"));
        File.Copy(
            Path.Combine(buildOutput, "Data", ProjectConstants.ProjectFileName),
            Path.Combine(replacementRoot, "Data", ProjectConstants.ProjectFileName));
        File.Copy(
            Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"),
            Path.Combine(replacementRoot, "Data", "graph.pompo-ir.json"));
        await File.WriteAllTextAsync(Path.Combine(replacementRoot, "extra.log"), "extra");
        File.Delete(manifest.ArchivePath);
        ZipFile.CreateFromDirectory(replacementRoot, manifest.ArchivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        var replacementHash = await ReleasePackageService.ComputeSha256Async(manifest.ArchivePath);
        var replacementManifest = manifest with
        {
            Sha256 = replacementHash,
            ArchiveSizeBytes = new FileInfo(manifest.ArchivePath).Length
        };
        await using (var stream = File.Create(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json")))
        {
            await JsonSerializer.SerializeAsync(stream, replacementManifest, ProjectFileService.CreateJsonOptions());
        }

        var verified = await service.VerifyAsync(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json"));

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL032");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsEmbeddedBuildManifestMismatch()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var replacementRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(replacementRoot, "Data"));
        File.Copy(
            Path.Combine(buildOutput, "Data", ProjectConstants.ProjectFileName),
            Path.Combine(replacementRoot, "Data", ProjectConstants.ProjectFileName));
        File.Copy(
            Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"),
            Path.Combine(replacementRoot, "Data", "graph.pompo-ir.json"));
        var mismatchedBuildManifest = new BuildArtifactManifest(
            "Different App",
            "0.1.0",
            PompoTargetPlatform.MacOS,
            false,
            ["Data/project.pompo.json", "Data/graph.pompo-ir.json"],
            ["graph.pompo-ir.json"],
            ["ko", "en"],
            ["ko", "en"]);
        await using (var embeddedStream = File.Create(Path.Combine(replacementRoot, "pompo-build-manifest.json")))
        {
            await JsonSerializer.SerializeAsync(embeddedStream, mismatchedBuildManifest, ProjectFileService.CreateJsonOptions());
        }

        File.Delete(manifest.ArchivePath);
        ZipFile.CreateFromDirectory(replacementRoot, manifest.ArchivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        var replacementHash = await ReleasePackageService.ComputeSha256Async(manifest.ArchivePath);
        var replacementManifest = manifest with
        {
            Sha256 = replacementHash,
            ArchiveSizeBytes = new FileInfo(manifest.ArchivePath).Length
        };
        await using (var stream = File.Create(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json")))
        {
            await JsonSerializer.SerializeAsync(stream, replacementManifest, ProjectFileService.CreateJsonOptions());
        }

        var verified = await service.VerifyAsync(Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json"));

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL025");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyCanRequireSmokeCoverageForEverySupportedLocale()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput, smokeTestedLocales: []);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");

        var defaultVerification = await service.VerifyAsync(manifestPath);
        var strictVerification = await service.VerifyAsync(
            manifestPath,
            new ReleaseVerificationOptions(RequireSmokeTestedLocales: true));

        Assert.Empty(manifest.SmokeTestedLocales);
        Assert.True(defaultVerification.IsValid);
        Assert.False(strictVerification.IsValid);
        Assert.Contains(strictVerification.Diagnostics, diagnostic => diagnostic.Code == "REL026");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyCanRequireSelfContainedRuntime()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput, selfContained: false);
        var service = new ReleasePackageService();
        await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");

        var defaultVerification = await service.VerifyAsync(manifestPath);
        var strictVerification = await service.VerifyAsync(
            manifestPath,
            new ReleaseVerificationOptions(RequireSelfContained: true));

        Assert.True(defaultVerification.IsValid);
        Assert.False(strictVerification.IsValid);
        Assert.Contains(strictVerification.Diagnostics, diagnostic => diagnostic.Code == "REL027");
    }

    [Fact]
    public async Task ReleasePackageService_StrictSelfContainedVerificationRequiresRuntimeExecutable()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput, selfContained: true);
        var service = new ReleasePackageService();
        await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");

        var verified = await service.VerifyAsync(
            manifestPath,
            new ReleaseVerificationOptions(RequireSelfContained: true));

        Assert.False(verified.IsValid);
        Assert.DoesNotContain(verified.Diagnostics, diagnostic => diagnostic.Code == "REL027");
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL029");
    }

    [Fact]
    public async Task ReleasePackageService_VerifyReportsMissingProjectDataAndCompiledGraphs()
    {
        var buildOutput = CreateTempDirectory();
        var releaseOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var service = new ReleasePackageService();
        var manifest = await service.PackageAsync(buildOutput, releaseOutput, "sample-0.1.0-macos");
        var tamperedManifest = manifest with
        {
            IncludedFiles = ["Data/graph.pompo-ir.json"],
            CompiledGraphs = []
        };
        var manifestPath = Path.Combine(releaseOutput, "sample-0.1.0-macos.release.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, tamperedManifest, ProjectFileService.CreateJsonOptions());
        }

        var verified = await service.VerifyAsync(manifestPath);

        Assert.False(verified.IsValid);
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL030");
        Assert.Contains(verified.Diagnostics, diagnostic => diagnostic.Code == "REL031");
    }

    [Fact]
    public async Task BuildHistoryService_RecordPersistsNewestEntriesFirst()
    {
        var root = CreateTempDirectory();
        var service = new BuildHistoryService();

        await service.RecordAsync(
            root,
            new BuildHistoryEntry(
                new DateTimeOffset(2026, 5, 5, 1, 0, 0, TimeSpan.Zero),
                "debug",
                PompoTargetPlatform.MacOS,
                "/tmp/build/debug",
                false,
                2,
                "Sample",
                "0.1.0"));
        await service.RecordAsync(
            root,
            new BuildHistoryEntry(
                new DateTimeOffset(2026, 5, 5, 2, 0, 0, TimeSpan.Zero),
                "release",
                PompoTargetPlatform.MacOS,
                "/tmp/build/release",
                true,
                0,
                "Sample",
                "1.0.0"));

        var loaded = await service.LoadAsync(root);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("release", loaded[0].ProfileName);
        Assert.Equal("debug", loaded[1].ProfileName);
        Assert.True(File.Exists(BuildHistoryService.GetHistoryPath(root)));
    }

    [Fact]
    public async Task BuildHistoryService_TrimsAndClearsHistory()
    {
        var root = CreateTempDirectory();
        var service = new BuildHistoryService();

        for (var index = 0; index < BuildHistoryService.MaxEntries + 3; index++)
        {
            await service.RecordAsync(
                root,
                new BuildHistoryEntry(
                    new DateTimeOffset(2026, 5, 5, index, 0, 0, TimeSpan.Zero),
                    $"profile-{index:00}",
                    PompoTargetPlatform.Linux,
                    $"/tmp/build/{index}",
                    true,
                    0,
                    "Sample",
                    "0.1.0"));
        }

        var loaded = await service.LoadAsync(root);

        Assert.Equal(BuildHistoryService.MaxEntries, loaded.Count);
        Assert.Equal("profile-22", loaded[0].ProfileName);
        Assert.DoesNotContain(loaded, entry => entry.ProfileName == "profile-00");

        await service.ClearAsync(root);

        Assert.Empty(await service.LoadAsync(root));
    }

    [Fact]
    public async Task BuildOutputVerificationService_AcceptsPackageableBuildOutput()
    {
        var buildOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);

        var result = await new BuildOutputVerificationService().VerifyAsync(
            buildOutput,
            new BuildOutputVerificationOptions(
                RequireSmokeTestedLocales: true,
                RequireSelfContained: false));

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Manifest);
    }

    [Fact]
    public async Task BuildOutputVerificationService_ReportsManifestAndOutputMismatch()
    {
        var buildOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        File.Delete(Path.Combine(buildOutput, "Data", "graph.pompo-ir.json"));
        await File.WriteAllTextAsync(Path.Combine(buildOutput, "Data", "extra.txt"), "extra");

        var result = await new BuildOutputVerificationService().VerifyAsync(buildOutput);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY020");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY021");
    }

    [Fact]
    public async Task BuildOutputVerificationService_RejectsSourceScriptArtifacts()
    {
        var buildOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var scriptPath = Path.Combine(buildOutput, "Scripts", "LeakedNode.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "public sealed class LeakedNode { }");
        await using (var stream = File.Create(Path.Combine(buildOutput, "pompo-build-manifest.json")))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new BuildArtifactManifest(
                    "Sample",
                    "0.1.0",
                    PompoTargetPlatform.MacOS,
                    false,
                    ["Data/project.pompo.json", "Data/graph.pompo-ir.json", "Scripts/LeakedNode.cs"],
                    ["graph.pompo-ir.json"],
                    ["ko", "en"],
                    ["ko", "en"]),
                ProjectFileService.CreateJsonOptions());
        }

        var result = await new BuildOutputVerificationService().VerifyAsync(buildOutput);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY026");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY027");
    }

    [Fact]
    public async Task BuildOutputVerificationService_RejectsForbiddenRuntimeArtifacts()
    {
        var buildOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var buildAssemblyPath = Path.Combine(buildOutput, "Runtime", "Pompo.Build.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(buildAssemblyPath)!);
        await File.WriteAllTextAsync(buildAssemblyPath, "build");
        await using (var stream = File.Create(Path.Combine(buildOutput, "pompo-build-manifest.json")))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new BuildArtifactManifest(
                    "Sample",
                    "0.1.0",
                    PompoTargetPlatform.MacOS,
                    false,
                    ["Data/project.pompo.json", "Data/graph.pompo-ir.json", "Runtime/Microsoft.CodeAnalysis.dll"],
                    ["graph.pompo-ir.json"],
                    ["ko", "en"],
                    ["ko", "en"]),
                ProjectFileService.CreateJsonOptions());
        }

        var result = await new BuildOutputVerificationService().VerifyAsync(buildOutput);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY009");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY022");
    }

    [Fact]
    public async Task BuildOutputVerificationService_RejectsDebugSymbolArtifacts()
    {
        var buildOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(buildOutput);
        var pdbPath = Path.Combine(buildOutput, "Runtime", "Pompo.Runtime.Fna.pdb");
        Directory.CreateDirectory(Path.GetDirectoryName(pdbPath)!);
        await File.WriteAllTextAsync(pdbPath, "symbols");
        await using (var stream = File.Create(Path.Combine(buildOutput, "pompo-build-manifest.json")))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new BuildArtifactManifest(
                    "Sample",
                    "0.1.0",
                    PompoTargetPlatform.MacOS,
                    false,
                    ["Data/project.pompo.json", "Data/graph.pompo-ir.json", "Runtime/Pompo.Runtime.Fna.pdb"],
                    ["graph.pompo-ir.json"],
                    ["ko", "en"],
                    ["ko", "en"]),
                ProjectFileService.CreateJsonOptions());
        }

        var result = await new BuildOutputVerificationService().VerifyAsync(buildOutput);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY028");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY029");
    }

    [Fact]
    public async Task BuildOutputVerificationService_ReportsStrictBuildRequirements()
    {
        var buildOutput = CreateTempDirectory();
        await WriteBuildManifestAsync(
            buildOutput,
            smokeTestedLocales: [],
            selfContained: false);

        var result = await new BuildOutputVerificationService().VerifyAsync(
            buildOutput,
            new BuildOutputVerificationOptions(
                RequireSmokeTestedLocales: true,
                RequireSelfContained: true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY023");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BVERIFY024");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteBuildManifestAsync(
        string buildOutput,
        bool writeGraphFile = true,
        IReadOnlyList<string>? smokeTestedLocales = null,
        bool selfContained = false,
        IReadOnlyList<string>? compiledGraphs = null,
        bool includeProjectData = true)
    {
        var dataDirectory = Path.Combine(buildOutput, "Data");
        Directory.CreateDirectory(dataDirectory);
        if (includeProjectData)
        {
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, ProjectConstants.ProjectFileName), "{}");
        }

        if (writeGraphFile)
        {
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "graph.pompo-ir.json"), "{}");
        }

        compiledGraphs ??= ["graph.pompo-ir.json"];
        var includedFiles = new List<string>();
        if (includeProjectData)
        {
            includedFiles.Add("Data/project.pompo.json");
        }

        includedFiles.AddRange(compiledGraphs.Select(graph => $"Data/{graph}"));

        var manifest = new BuildArtifactManifest(
            "Sample",
            "0.1.0",
            PompoTargetPlatform.MacOS,
            selfContained,
            includedFiles,
            compiledGraphs,
            ["ko", "en"],
            smokeTestedLocales ?? ["ko", "en"]);

        await using var stream = File.Create(Path.Combine(buildOutput, "pompo-build-manifest.json"));
        await JsonSerializer.SerializeAsync(stream, manifest, ProjectFileService.CreateJsonOptions());
    }

    private static PompoTargetPlatform CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return PompoTargetPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return PompoTargetPlatform.MacOS;
        }

        return PompoTargetPlatform.Linux;
    }

    private static GraphDocument LocalizedGraph()
    {
        var start = new GraphNode(
            "start",
            GraphNodeKind.Start,
            new GraphPoint(0, 0),
            [NodeCatalog.OutExecPort()],
            []);
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject
            {
                ["tableId"] = "dialogue",
                ["textKey"] = "line.hello",
                ["text"] = "Hello"
            });
        var end = new GraphNode(
            "end",
            GraphNodeKind.EndScene,
            new GraphPoint(400, 0),
            [NodeCatalog.InExecPort()],
            []);

        return new GraphDocument(
            1,
            "intro",
            [start, line, end],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "end", "in")
            ]);
    }

    private static GraphDocument CallGraphRoot()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []);
        var call = new GraphNode(
            "call_child",
            GraphNodeKind.CallGraph,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["graphId"] = "child" });
        var end = new GraphNode("end", GraphNodeKind.EndScene, new GraphPoint(400, 0), [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "root",
            [start, call, end],
            [
                new GraphEdge("e1", "start", "out", "call_child", "in"),
                new GraphEdge("e2", "call_child", "out", "end", "in")
            ]);
    }

    private static GraphDocument CallGraphChild()
    {
        var start = new GraphNode("start", GraphNodeKind.Start, new GraphPoint(0, 0), [NodeCatalog.OutExecPort()], []);
        var line = new GraphNode(
            "line",
            GraphNodeKind.Narration,
            new GraphPoint(200, 0),
            [NodeCatalog.InExecPort(), NodeCatalog.OutExecPort()],
            new JsonObject { ["text"] = "Child smoke" });
        var ret = new GraphNode("return", GraphNodeKind.Return, new GraphPoint(400, 0), [NodeCatalog.InExecPort()], []);

        return new GraphDocument(
            1,
            "child",
            [start, line, ret],
            [
                new GraphEdge("e1", "start", "out", "line", "in"),
                new GraphEdge("e2", "line", "out", "return", "in")
            ]);
    }
}
