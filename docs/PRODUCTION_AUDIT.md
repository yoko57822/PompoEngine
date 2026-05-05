# Production Audit

This audit maps the original PompoEngine objective to concrete repository
evidence. Keep it current before claiming release readiness.

## Objective

Prepare PompoEngine for open-source publication and continue hardening it toward
production use as a PC-only visual novel engine with an Avalonia editor, FNA
runtime, VN-specific visual scripting, safe C# extensions, release packaging,
and user-facing documentation.

## Prompt-To-Artifact Checklist

| Requirement | Evidence | Current status |
| --- | --- | --- |
| Open-source repository baseline | `LICENSE`, `CHANGELOG.md`, `MAINTAINERS.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SUPPORT.md`, `SECURITY.md`, `.editorconfig`, `.gitattributes`, `.gitignore`, `.ignore`, `.github/dependabot.yml`, `.github/ISSUE_TEMPLATE/bug_report.md`, `.github/ISSUE_TEMPLATE/feature_request.md`, `.github/ISSUE_TEMPLATE/config.yml`, `.github/pull_request_template.md` | Present |
| Fixed .NET 10 toolchain | `global.json`, `Directory.Build.props`, `dotnet --version` in CI environment | Present locally; CI validates via restore/build/test |
| Architecture and module boundaries | `docs/ARCHITECTURE.md`, solution layout, repository doctor project-boundary checks | Present |
| PC desktop target only | README requirements and build platforms limited to Windows, macOS, Linux | Present |
| Avalonia editor entry point | `src/Pompo.Editor.Avalonia`, README run command, editor ViewModel tests | Present |
| FNA runtime entry point | `src/Pompo.Runtime.Fna`, runtime validation command, runtime tests | Present |
| JSON project format and folders | `Pompo.Core` project models, schema v1 to v7 migrations, template service, project file tests | Present |
| VN graph authoring and IR compilation | `Pompo.VisualScripting`, graph authoring/editor tests, compiler tests | Present |
| Runtime VN playback MVP | runtime interpreter, project-configurable UI layout, runtime UI theme colors, runtime UI image skin slots including selected/disabled choice states, runtime UI animation settings with typing reveal, runtime playback timing settings, disabled choice widget states, BGM/SFX/voice audio state, mouse hover choice and save-slot highlighting, validation, save, localization, asset catalog, and CLI trace tests | Present |
| Editor authoring workflow | workspace ViewModel tests covering project, scene, graph, localization, theme/skin/layout/animation validation, layout reset, persisted workspace layout presets, focus targets, panel visibility toggles, preview, save slots, build panel, help surface, plus draggable/resizable layout preview control, resizable/collapsible workspace panels, and detached Project/Scene/Graph/Inspector/Preview/Console window support | Present |
| Build pipeline | `Pompo.Build`, build profile/history/output verification tests | Present |
| Release packaging | release package/verify/audit/sign commands, release service tests, `.github/workflows/release.yml` | Present |
| Crash-safe local writes | `AtomicFileWriter` is used for project JSON, graph JSON, build profiles, editor preferences, docs site output, release manifests/signatures, imported asset copies, and build IR/manifest output | Present |
| Local release gate runner | `scripts/check-release-gates.sh` and `scripts/check-release-gates.ps1` wrap restore, build, test, CLI/runtime version metadata, docs site, repository doctor, and runtime validation; repository doctor verifies Unix executable permissions for the shell gate | Present |
| Least-privilege workflow permissions | CI and package jobs use `contents: read`; only GitHub Release publishing uses `contents: write` | Present |
| Dependency update safety | `.github/dependabot.yml` and `.github/workflows/dependency-review.yml` check NuGet, GitHub Actions, and PR dependency changes | Present |
| Static security analysis | `.github/workflows/codeql.yml` runs CodeQL for C# with explicit security-events permission | Present |
| Runtime artifact separation | build/release verifiers reject editor, Avalonia, build, CLI, test, Roslyn, source script, and debug symbol artifacts | Present |
| Safe C# extension API | `Pompo.Scripting`, script compile/security/runtime-node tests, `docs/SCRIPTING.md`, `docs/COMPATIBILITY.md`, security docs | Present |
| Sample VN validation | sample template, CI sample project build, local sample build smoke commands | Present |
| Korean execution and usage docs | `docs/RUN_AND_USE.md`, linked from `README.md` | Present |
| Contributor development workflow | `docs/DEVELOPMENT.md`, `CONTRIBUTING.md`, local gate scripts, and repository doctor documentation checks | Present |
| Troubleshooting workflow | `docs/TROUBLESHOOTING.md`, repository doctor token checks, generated docs site | Present |
| Generated documentation site | `docs site --root . --output artifacts/docs-site --json`, CI gate, GitHub Pages workflow, `DocumentationSiteService` tests | Present |
| Compatibility and release process docs | `docs/COMPATIBILITY.md`, `docs/RELEASING.md`, `docs/OPEN_SOURCE_RELEASE_CHECKLIST.md` | Present |

## Required Gates

Before publishing a release candidate, run:

```bash
scripts/check-release-gates.sh
```

PowerShell equivalent:

```powershell
pwsh scripts/check-release-gates.ps1
```

This expands to the required local gates:

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

For build or release changes, also run the sample project flow listed in
`docs/OPEN_SOURCE_RELEASE_CHECKLIST.md` and verify the release manifest with
`--require-smoke-tested-locales` and `--require-self-contained`. Once a release
manifest exists, run the final readiness audit:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest <releaseManifestJson> --require-smoke-tested-locales --require-self-contained --json
```

## Completion Assessment

PompoEngine is suitable for open-source publication as a pre-1.0 engine
foundation with automated validation, release packaging, and a documented usage
path. It should not be described as a fully stable 1.0 production engine yet.

The remaining production risks are:

- Runtime UI supports project-configurable theme colors, image skin slots,
  nine-slice panel rendering, layout geometry, disabled choice states,
  panel fade, selected-choice pulse, typing reveal, and playback timing. The
  remaining skinning gap is higher-level widget behavior and
  richer animation authoring beyond the current numeric and preset timing
  controls, previewed fade/pulse state, drag-positioning, and resize handles.
- Editor UI includes functional authoring, build, localization, save, preview,
  theme tabs, resizable/collapsible workspace panels, workflow focus targets,
  persisted workflow-oriented workspace layout presets, and detached
  Project/Scene/Graph/Inspector/Preview/Console windows, but is not yet a full
  designer-grade detachable docking experience.
- The project schema is versioned and now exercises migration from schema v1 to
  schema v7 for runtime UI theme, skin, layout, animation, playback defaults, and disabled choice skin slots; future
  schema changes still need the same migration coverage.
- A dedicated documentation website can be generated from repository Markdown
  and has a GitHub Pages workflow; final publication still needs live Pages
  evidence after the repository is public.
- Cross-platform release artifacts are verified by CI workflow definitions and
  local current-platform smoke tests, but each platform still needs live CI
  evidence on the public repository before a final release.

These items belong in the roadmap until they have direct implementation and
verification evidence.
