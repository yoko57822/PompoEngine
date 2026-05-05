using Pompo.Build;

namespace Pompo.Tests;

public sealed class DocumentationSiteServiceTests
{
    [Fact]
    public async Task GenerateAsync_WritesIndexPagesAndManifest()
    {
        var root = CreateTempDirectory();
        var output = Path.Combine(root, "artifacts", "docs-site");
        await WriteDocumentationSourcesAsync(root);

        var manifest = await new DocumentationSiteService().GenerateAsync(root, output);

        Assert.Equal(18, manifest.Pages.Count);
        Assert.True(File.Exists(Path.Combine(output, "index.html")));
        Assert.True(File.Exists(Path.Combine(output, "pompo-docs-site.json")));
        Assert.Contains(manifest.Pages, page => page.SourcePath == "README.md" && page.OutputPath == "pages/readme.html");
        var index = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
        Assert.Contains("PompoEngine Documentation", index);
        Assert.Contains("pages/readme.html", index);
        var readme = await File.ReadAllTextAsync(Path.Combine(output, "pages", "readme.html"));
        Assert.Contains("<h1>PompoEngine</h1>", readme);
        Assert.Contains("<li>Open source ready</li>", readme);
    }

    internal static async Task WriteDocumentationSourcesAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "README.md"),
            """
            # PompoEngine

            - Open source ready
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "CONTRIBUTING.md"), "# Contributing");
        await File.WriteAllTextAsync(Path.Combine(root, "CHANGELOG.md"), "# Changelog");
        await File.WriteAllTextAsync(Path.Combine(root, "MAINTAINERS.md"), "# Maintainers");
        await File.WriteAllTextAsync(Path.Combine(root, "CODE_OF_CONDUCT.md"), "# Code of Conduct");
        await File.WriteAllTextAsync(Path.Combine(root, "SUPPORT.md"), "# Support");
        await File.WriteAllTextAsync(Path.Combine(root, "SECURITY.md"), "# Security");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "GETTING_STARTED.md"), "# Getting Started");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "RUN_AND_USE.md"), "# Run and Use");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "DEVELOPMENT.md"), "# Development");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "TROUBLESHOOTING.md"), "# Troubleshooting");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "ARCHITECTURE.md"), "# Architecture");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "PRODUCTION_AUDIT.md"), "# Production Audit");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "OPEN_SOURCE_RELEASE_CHECKLIST.md"), "# Release Checklist");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "RELEASING.md"), "# Releasing");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "ROADMAP.md"), "# Roadmap");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "COMPATIBILITY.md"), "# Compatibility");
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "SCRIPTING.md"), "# Scripting");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
