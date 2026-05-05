# Development Guide

This guide is for contributors working on PompoEngine itself.

## Local Gate

Run the repository gate before opening a pull request:

```bash
scripts/check-release-gates.sh
```

PowerShell equivalent:

```powershell
pwsh scripts/check-release-gates.ps1
```

The gate restores the solution, builds, runs tests, captures CLI and runtime
version metadata, generates the documentation site, runs repository doctor, and
validates the runtime host.

## Common Commands

```bash
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx --no-restore
dotnet test PompoEngine.slnx --no-build
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime
```

## Project Boundaries

- `Pompo.Core` stays framework-neutral and must not reference editor, runtime,
  build, CLI, scripting, FNA, or Avalonia packages.
- `Pompo.Runtime.Fna` must not reference Avalonia, editor UI, build tooling, or
  CLI projects.
- `Pompo.VisualScripting` must not reference editor, runtime, build, CLI, FNA,
  or Avalonia packages.
- Executable projects must set `IsPackable=false` unless a dedicated
  distribution package is added.

Repository doctor enforces these boundaries.

## Change Checklist

- Runtime, graph, scripting, build, or schema changes need focused tests.
- User-facing behavior changes need README or `docs/` updates.
- Project schema changes need migration behavior and compatibility notes.
- Release and workflow changes must keep `scripts/check-release-gates.sh`,
  `scripts/check-release-gates.ps1`, repository doctor, and docs-site
  generation passing.
- Generated outputs such as `bin/`, `obj/`, `TestResults/`, `artifacts/`,
  `dist/`, `.DS_Store`, `Thumbs.db`, and `Desktop.ini` must not be committed.

## Documentation Site

The docs site is generated from a fixed source list in
`DocumentationSiteService`. When a public-facing document is added, update:

- `src/Pompo.Build/DocumentationSiteService.cs`
- `tests/Pompo.Tests/DocumentationSiteServiceTests.cs`
- `src/Pompo.Build/RepositoryDoctorService.cs`
- `tests/Pompo.Tests/RepositoryDoctorServiceTests.cs`
- `README.md`
