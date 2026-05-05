# Open Source Release Checklist

Use this checklist before making PompoEngine public or cutting a tagged release.

## Repository Identity

- Replace placeholder repository owner/contact text in GitHub settings, `SECURITY.md`, and release notes.
- Confirm the public repository URL before adding `RepositoryUrl` or `PackageProjectUrl` to MSBuild metadata.
- Keep the MIT license and contributor attribution in sync with the repository owner policy.

## Required Local Validation

Run the local gate script for the standard repository checks:

```bash
scripts/check-release-gates.sh
```

On Windows or PowerShell-based shells, run:

```powershell
pwsh scripts/check-release-gates.ps1
```

The script expands to:

```bash
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx --no-restore
dotnet test PompoEngine.slnx --no-build
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- version --json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime
```

For build or release changes, also run a sample project through:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/PompoSample --name PompoSample --template sample --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization report --project /tmp/PompoSample --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/PompoSample
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/PompoSample --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/PompoSample --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/PompoSample --profile-file /tmp/PompoSample/BuildProfiles/release.pompo-build.json --output /tmp/PompoSample/Builds
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/PompoSample/Builds/MacOS/release --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release package --build /tmp/PompoSample/Builds/MacOS/release --output /tmp/PompoSample/Releases --name PompoSample-0.1.0-macos --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest /tmp/PompoSample/Releases/PompoSample-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history list --project /tmp/PompoSample --json
```

## Release Gates

- CI must pass on macOS with restore, build, tests, CLI metadata smoke, runtime metadata smoke, packaged runtime smoke, and runtime validation.
- `scripts/check-release-gates.sh` or `scripts/check-release-gates.ps1` must pass locally before tagging.
- On macOS/Linux, repository doctor must confirm `scripts/check-release-gates.sh` has executable permissions.
- Tag releases must produce Windows, macOS, and Linux sample runtime archives.
- Release verification must require smoke-tested locales and self-contained runtime output.
- `release audit` must pass with the generated docs site and the release manifest before publication.
- Runtime archives must not contain `Pompo.Editor.*`, `Avalonia*`, `Pompo.Build*`, `Pompo.Cli*`, `Pompo.Tests*`, `Microsoft.CodeAnalysis*`, `xunit*`, `testhost*`, source script files, or debug symbols.
- Release package names must be safe file names using only letters, numbers, `-`, `_`, and `.`.
- Asset IDs must be safe file names using only letters, numbers, `-`, `_`, and `.`.
- Graph IDs must be safe file names because build outputs write graph IR files from them.
- Release signing secrets should be configured together or omitted together.

## Documentation Gates

- `README.md` must describe the current milestone honestly and avoid claiming 1.0 stability.
- `docs/DEVELOPMENT.md` must describe local gates, project boundaries, and documentation-site update requirements.
- `docs/PRODUCTION_AUDIT.md` must map the objective to concrete repository evidence and remaining risks.
- `docs/ROADMAP.md` must show remaining production blockers.
- `docs/RELEASING.md` must match the current release workflow and CLI flags.
- `docs site --root . --output artifacts/docs-site --json` must generate `index.html`, page HTML files, and `pompo-docs-site.json`.
- `.github/workflows/docs.yml` must publish `artifacts/docs-site` to GitHub Pages.
- User-facing project format changes must include migration or compatibility notes.

## Packaging Gates

- Keep executable projects non-packable unless a dedicated distribution package is added.
- Do not publish NuGet packages until package IDs, repository URL, API compatibility policy, and symbol/source publishing are finalized.
- Confirm generated `bin/`, `obj/`, `TestResults/`, `artifacts/`, and `dist/` outputs are excluded from commits.
