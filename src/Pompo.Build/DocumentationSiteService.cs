using System.Net;
using System.Text;
using System.Text.Json;
using Pompo.Core.Project;

namespace Pompo.Build;

public sealed record DocumentationSitePage(
    string Title,
    string SourcePath,
    string OutputPath);

public sealed record DocumentationSiteManifest(
    string RepositoryRoot,
    string OutputDirectory,
    IReadOnlyList<DocumentationSitePage> Pages);

public sealed class DocumentationSiteService
{
    private static readonly (string Path, string Title)[] SourceDocuments =
    [
        ("README.md", "Overview"),
        ("docs/GETTING_STARTED.md", "Getting Started"),
        ("docs/RUN_AND_USE.md", "Run and Use"),
        ("docs/DEVELOPMENT.md", "Development"),
        ("docs/TROUBLESHOOTING.md", "Troubleshooting"),
        ("docs/ARCHITECTURE.md", "Architecture"),
        ("docs/PRODUCTION_AUDIT.md", "Production Audit"),
        ("docs/OPEN_SOURCE_RELEASE_CHECKLIST.md", "Release Checklist"),
        ("docs/RELEASING.md", "Releasing"),
        ("docs/ROADMAP.md", "Roadmap"),
        ("docs/COMPATIBILITY.md", "Compatibility"),
        ("docs/SCRIPTING.md", "Scripting"),
        ("CHANGELOG.md", "Changelog"),
        ("MAINTAINERS.md", "Maintainers"),
        ("CONTRIBUTING.md", "Contributing"),
        ("CODE_OF_CONDUCT.md", "Code of Conduct"),
        ("SUPPORT.md", "Support"),
        ("SECURITY.md", "Security")
    ];

    public async Task<DocumentationSiteManifest> GenerateAsync(
        string repositoryRoot,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var root = Path.GetFullPath(repositoryRoot);
        var output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "pages"));

        var pages = new List<DocumentationSitePage>();
        foreach (var (relativePath, title) in SourceDocuments)
        {
            var sourcePath = Path.Combine(root, relativePath);
            if (!File.Exists(sourcePath))
            {
                throw new InvalidDataException($"Documentation source '{relativePath}' does not exist.");
            }

            var markdown = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var outputPath = Path.Combine("pages", $"{CreateSlug(relativePath)}.html");
            await AtomicFileWriter.WriteTextAsync(
                    Path.Combine(output, outputPath),
                    RenderPage(title, relativePath, markdown),
                    cancellationToken)
                .ConfigureAwait(false);
            pages.Add(new DocumentationSitePage(title, relativePath, outputPath.Replace('\\', '/')));
        }

        var manifest = new DocumentationSiteManifest(root, output, pages);
        await AtomicFileWriter.WriteTextAsync(
                Path.Combine(output, "index.html"),
                RenderIndex(manifest),
                cancellationToken)
            .ConfigureAwait(false);
        await AtomicFileWriter.WriteTextAsync(
                Path.Combine(output, "pompo-docs-site.json"),
                JsonSerializer.Serialize(manifest, ProjectFileService.CreateJsonOptions()),
                cancellationToken)
            .ConfigureAwait(false);

        return manifest;
    }

    private static string RenderIndex(DocumentationSiteManifest manifest)
    {
        var links = string.Join(
            Environment.NewLine,
            manifest.Pages.Select(page =>
                $"""<li><a href="{WebUtility.HtmlEncode(page.OutputPath)}">{WebUtility.HtmlEncode(page.Title)}</a><span>{WebUtility.HtmlEncode(page.SourcePath)}</span></li>"""));
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>PompoEngine Documentation</title>
              <style>{{Css}}</style>
            </head>
            <body>
              <main class="shell">
                <header>
                  <p class="eyebrow">PompoEngine</p>
                  <h1>Documentation</h1>
                  <p>Generated from repository Markdown for release review and open-source publication.</p>
                </header>
                <section class="panel">
                  <h2>Documents</h2>
                  <ul class="doc-list">
                    {{links}}
                  </ul>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string RenderPage(string title, string sourcePath, string markdown)
    {
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{WebUtility.HtmlEncode(title)}} - PompoEngine Documentation</title>
              <style>{{Css}}</style>
            </head>
            <body>
              <main class="shell">
                <nav><a href="../index.html">Documentation</a> / {{WebUtility.HtmlEncode(sourcePath)}}</nav>
                <article class="panel markdown">
                  {{RenderMarkdown(markdown)}}
                </article>
              </main>
            </body>
            </html>
            """;
    }

    private static string RenderMarkdown(string markdown)
    {
        var html = new StringBuilder();
        var inCode = false;
        var inUnorderedList = false;
        var inOrderedList = false;
        foreach (var rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                CloseLists();
                if (inCode)
                {
                    html.AppendLine("</code></pre>");
                    inCode = false;
                }
                else
                {
                    html.AppendLine("<pre><code>");
                    inCode = true;
                }

                continue;
            }

            if (inCode)
            {
                html.AppendLine(WebUtility.HtmlEncode(line));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                CloseLists();
                continue;
            }

            var headingLevel = line.TakeWhile(character => character == '#').Count();
            if (headingLevel is >= 1 and <= 6 && line.Length > headingLevel && line[headingLevel] == ' ')
            {
                CloseLists();
                var heading = WebUtility.HtmlEncode(line[(headingLevel + 1)..].Trim());
                html.AppendLine($"<h{headingLevel}>{heading}</h{headingLevel}>");
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!inUnorderedList)
                {
                    CloseOrderedList();
                    html.AppendLine("<ul>");
                    inUnorderedList = true;
                }

                html.AppendLine($"<li>{WebUtility.HtmlEncode(line[2..].Trim())}</li>");
                continue;
            }

            var orderedMarker = GetOrderedListMarkerLength(line);
            if (orderedMarker > 0)
            {
                if (!inOrderedList)
                {
                    CloseUnorderedList();
                    html.AppendLine("<ol>");
                    inOrderedList = true;
                }

                html.AppendLine($"<li>{WebUtility.HtmlEncode(line[orderedMarker..].Trim())}</li>");
                continue;
            }

            CloseLists();
            html.AppendLine($"<p>{WebUtility.HtmlEncode(line)}</p>");
        }

        if (inCode)
        {
            html.AppendLine("</code></pre>");
        }

        CloseLists();
        return html.ToString();

        void CloseLists()
        {
            CloseUnorderedList();
            CloseOrderedList();
        }

        void CloseUnorderedList()
        {
            if (!inUnorderedList)
            {
                return;
            }

            html.AppendLine("</ul>");
            inUnorderedList = false;
        }

        void CloseOrderedList()
        {
            if (!inOrderedList)
            {
                return;
            }

            html.AppendLine("</ol>");
            inOrderedList = false;
        }
    }

    private static int GetOrderedListMarkerLength(string line)
    {
        var digits = line.TakeWhile(char.IsDigit).Count();
        return digits > 0 &&
            line.Length > digits + 1 &&
            line[digits] == '.' &&
            line[digits + 1] == ' '
            ? digits + 2
            : 0;
    }

    private static string CreateSlug(string relativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        return string.Concat(fileName.Select(character =>
            char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-')).Trim('-');
    }

    private const string Css = """
        :root { color-scheme: light; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
        body { margin: 0; background: #f4f5f7; color: #172033; }
        a { color: #1d4ed8; text-decoration: none; }
        a:hover { text-decoration: underline; }
        .shell { width: min(1080px, calc(100vw - 40px)); margin: 0 auto; padding: 42px 0 64px; }
        header { margin-bottom: 24px; }
        .eyebrow { margin: 0 0 8px; color: #526071; font-size: 13px; font-weight: 700; text-transform: uppercase; }
        h1 { margin: 0 0 10px; font-size: 38px; line-height: 1.12; }
        h2 { margin-top: 0; }
        nav { margin-bottom: 14px; color: #526071; font-size: 14px; }
        .panel { background: #fff; border: 1px solid #d9dee7; border-radius: 8px; padding: 24px; box-shadow: 0 10px 26px rgba(23, 32, 51, 0.06); }
        .doc-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
        .doc-list li { border: 1px solid #e4e8f0; border-radius: 6px; padding: 12px 14px; display: flex; justify-content: space-between; gap: 16px; }
        .doc-list span { color: #526071; font-size: 13px; }
        .markdown h1, .markdown h2, .markdown h3 { margin-top: 26px; }
        .markdown h1:first-child, .markdown h2:first-child, .markdown h3:first-child { margin-top: 0; }
        .markdown p, .markdown li { line-height: 1.62; }
        pre { overflow-x: auto; background: #111827; color: #e5e7eb; border-radius: 6px; padding: 16px; }
        code { font-family: "SFMono-Regular", Consolas, monospace; }
        """;
}
