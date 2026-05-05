namespace Pompo.Build;

public sealed record ReleaseAuditGate(
    string Gate,
    bool Passed,
    string Message,
    string? Path = null);

public sealed record ReleaseAuditResult(
    string RepositoryRoot,
    string? ReleaseManifestPath,
    IReadOnlyList<ReleaseAuditGate> Gates)
{
    public bool IsReady => Gates.All(gate => gate.Passed);
}

public sealed class ReleaseAuditService
{
    public async Task<ReleaseAuditResult> InspectAsync(
        string repositoryRoot,
        string? releaseManifestPath = null,
        ReleaseVerificationOptions? releaseVerificationOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var gates = new List<ReleaseAuditGate>();
        var repositoryDoctor = await new RepositoryDoctorService()
            .InspectAsync(repositoryRoot, cancellationToken)
            .ConfigureAwait(false);

        if (repositoryDoctor.IsHealthy)
        {
            gates.Add(new ReleaseAuditGate(
                "repository-doctor",
                true,
                "Repository open-source hygiene checks passed.",
                repositoryRoot));
        }
        else
        {
            gates.AddRange(repositoryDoctor.Diagnostics.Select(diagnostic =>
                new ReleaseAuditGate(
                    "repository-doctor",
                    false,
                    $"{diagnostic.Code}: {diagnostic.Message}",
                    diagnostic.Path)));
        }

        ValidateDocsSiteArtifacts(repositoryRoot, gates);

        if (string.IsNullOrWhiteSpace(releaseManifestPath))
        {
            gates.Add(new ReleaseAuditGate(
                "release-manifest",
                false,
                "A release manifest is required for final release audit. Run 'release package' and pass --manifest.",
                null));
        }
        else
        {
            var releaseResult = await new ReleasePackageService()
                .VerifyAsync(releaseManifestPath, releaseVerificationOptions ?? new ReleaseVerificationOptions())
                .ConfigureAwait(false);

            if (releaseResult.IsValid)
            {
                gates.Add(new ReleaseAuditGate(
                    "release-manifest",
                    true,
                    $"Release manifest '{releaseResult.Manifest!.PackageName}' passed verification.",
                    releaseManifestPath));
            }
            else
            {
                gates.AddRange(releaseResult.Diagnostics.Select(diagnostic =>
                    new ReleaseAuditGate(
                        "release-manifest",
                        false,
                        $"{diagnostic.Code}: {diagnostic.Message}",
                        releaseManifestPath)));
            }
        }

        return new ReleaseAuditResult(repositoryRoot, releaseManifestPath, gates);
    }

    private static void ValidateDocsSiteArtifacts(
        string repositoryRoot,
        ICollection<ReleaseAuditGate> gates)
    {
        var docsSiteRoot = Path.Combine(repositoryRoot, "artifacts", "docs-site");
        var indexPath = Path.Combine(docsSiteRoot, "index.html");
        var manifestPath = Path.Combine(docsSiteRoot, "pompo-docs-site.json");
        if (File.Exists(indexPath) && File.Exists(manifestPath))
        {
            gates.Add(new ReleaseAuditGate(
                "docs-site",
                true,
                "Generated documentation site artifacts exist.",
                docsSiteRoot));
            return;
        }

        gates.Add(new ReleaseAuditGate(
            "docs-site",
            false,
            "Generated documentation site artifacts are missing. Run 'docs site --root . --output artifacts/docs-site --json'.",
            docsSiteRoot));
    }
}
