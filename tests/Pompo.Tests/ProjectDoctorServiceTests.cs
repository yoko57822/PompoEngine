using Pompo.Build;
using Pompo.Core.Project;

namespace Pompo.Tests;

public sealed class ProjectDoctorServiceTests
{
    [Fact]
    public async Task InspectAsync_AcceptsSampleTemplate()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateSampleVisualNovelAsync(root, "Doctor Sample");

        var result = await new ProjectDoctorService().InspectAsync(root);

        Assert.True(result.IsHealthy);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task InspectAsync_ReportsProjectValidationDiagnostics()
    {
        var root = CreateTempDirectory();
        await new ProjectFileService().SaveAsync(
            root,
            new PompoProjectDocument
            {
                ProjectName = "Empty",
                Graphs = []
            });

        var result = await new ProjectDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "POMPO026");
    }

    [Fact]
    public async Task InspectAsync_ReportsReleaseProfileHardeningDiagnostics()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Loose Release");
        await new BuildProfileFileService().SaveAsync(
            BuildProfileFileService.GetDefaultProfilePath(root, "release"),
            new PompoBuildProfile(
                "release",
                PompoTargetPlatform.MacOS,
                "Loose Release",
                "0.1.0",
                RunSmokeTest: false,
                PackageRuntime: false,
                SelfContained: false));

        var result = await new ProjectDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR006");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR007");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR008");
    }

    [Fact]
    public async Task InspectAsync_ReportsInvalidAdditionalBuildProfiles()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Broken Profiles");
        var profileDirectory = BuildProfileFileService.GetProfileDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(profileDirectory, "broken.pompo-build.json"),
            "{ not json");
        await new BuildProfileFileService().SaveAsync(
            Path.Combine(profileDirectory, "mismatch.pompo-build.json"),
            new PompoBuildProfile(
                "other",
                PompoTargetPlatform.MacOS,
                "Broken Profiles",
                "0.1.0"));
        await File.WriteAllTextAsync(
            Path.Combine(profileDirectory, "bad name.pompo-build.json"),
            "{}");

        var result = await new ProjectDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR005" && diagnostic.Path?.EndsWith("broken.pompo-build.json", StringComparison.Ordinal) == true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR009" && diagnostic.Path?.EndsWith("bad name.pompo-build.json", StringComparison.Ordinal) == true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR011" && diagnostic.Path?.EndsWith("mismatch.pompo-build.json", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task InspectAsync_ReportsRequiredBuildProfileNameMismatch()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Required Profile Mismatch");
        await new BuildProfileFileService().SaveAsync(
            BuildProfileFileService.GetDefaultProfilePath(root, "debug"),
            new PompoBuildProfile(
                "debug-copy",
                PompoTargetPlatform.MacOS,
                "Required Profile Mismatch",
                "0.1.0"));

        var result = await new ProjectDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR011" && diagnostic.Path?.EndsWith("debug.pompo-build.json", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task InspectAsync_ReportsBuildProfileMetadataDiagnostics()
    {
        var root = CreateTempDirectory();
        await new ProjectTemplateService().CreateMinimalVisualNovelAsync(root, "Broken Profile Metadata");
        await new BuildProfileFileService().SaveProjectProfileAsync(
            root,
            new PompoBuildProfile(
                "metadata",
                PompoTargetPlatform.MacOS,
                "",
                "",
                IconPath: "missing-icon.png",
                PackageRuntime: true,
                RuntimeProjectPath: "missing-runtime.csproj"));

        var result = await new ProjectDoctorService().InspectAsync(root);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR012" && diagnostic.Path?.EndsWith("metadata.pompo-build.json", StringComparison.Ordinal) == true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR013" && diagnostic.Path?.EndsWith("metadata.pompo-build.json", StringComparison.Ordinal) == true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR014" && diagnostic.Path?.EndsWith("metadata.pompo-build.json", StringComparison.Ordinal) == true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCTOR015" && diagnostic.Path?.EndsWith("metadata.pompo-build.json", StringComparison.Ordinal) == true);
    }

    private static string CreateTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
    }
}
