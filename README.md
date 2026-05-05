# PompoEngine

PompoEngine is a PC-only visual novel engine prototype.

The default workflow is:

1. Create or open a project in `Pompo.Editor.Avalonia`.
2. Author scenes and VN-specific visual graphs.
3. Preview through the isolated FNA runtime path.
4. Build standalone Windows, macOS, or Linux output through `Pompo.Build`.

## Solution Layout

- `Pompo.Core`: shared project JSON, asset database, scene, character, graph, save, and runtime data contracts.
- `Pompo.VisualScripting`: VN node catalog, graph validation, traversal, and compiled `PompoGraphIR`.
- `Pompo.Runtime.Fna`: minimal FNA runtime host and headless IR runner. It has no Avalonia/editor references and loads packaged user script nodes when present.
- `Pompo.Editor.Avalonia`: Avalonia desktop editor shell with project, scene, inspector, graph, preview, console, build, and help surfaces.
- `Pompo.Build`: profile-based build pipeline that validates projects, compiles graphs to IR, compiles safe user scripts, copies assets, and writes a build manifest with locale and smoke-test traceability.
- `Pompo.Scripting`: advanced C# extension API and Roslyn-based user script compiler with default safety gates.
- `Pompo.Cli`: command-line entry point for project creation, doctor checks, validation, and build automation.
- `Pompo.Tests`: unit tests for project files, graph validation/compilation, build packaging boundaries, and scripting security.

## Requirements

- .NET SDK `10.0.100`, fixed by `global.json`.
- Desktop target only. Mobile, web, and console are intentionally out of scope.

## Commands

```bash
scripts/check-release-gates.sh
pwsh scripts/check-release-gates.ps1
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx
dotnet test PompoEngine.slnx
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/MyVN --name MyVN --template sample --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset import --project /tmp/MyVN --file ./background.png --type Image --asset-id bg-main --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset delete --project /tmp/MyVN --asset-id unused-bg --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization report --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization add-locale --project /tmp/MyVN --locale ja --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization delete-locale --project /tmp/MyVN --locale ja --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization repair --project /tmp/MyVN --fallback-locale ko --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- version --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile save --project /tmp/MyVN --name demo --platform MacOS --app-name MyVN --version 0.1.0 --data-only --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --output /tmp/MyVN/Builds
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/MyVN/Builds/MacOS/release --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project /tmp/MyVN --type Image --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release package --build /tmp/MyVN/Builds/MacOS/release --output /tmp/MyVN/Releases --name MyVN-0.1.0-macos --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release sign --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --private-key ./release-private.pem --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify-signature --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --public-key ./release-public.pem --json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --choices 1 --json-trace
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --locale ko --choices 1 --json-trace
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --run-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --choices 1 --saves /tmp/MyVN/Saves
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save list --saves /tmp/MyVN/Saves --json
dotnet run --project src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj
```

## Current Milestone

This repository currently implements the phase-1 foundation plus the first contracts needed by phases 2, 5, 7, and 8:

- .NET 10 solution and project boundaries.
- FNA runtime host with 1920x1080 letterbox canvas logic.
- Runtime dialogue box, name box, choice box, text wrapping, configurable typing reveal, per-line progression, mouse hover choice highlighting, mouse/keyboard choice selection, auto-forward, skip mode, and small built-in bitmap font rendering without XNA Content Pipeline.
- Runtime UI theme colors are configurable from `project.pompo.json` through `runtimeUiTheme`, with safe fallback colors for invalid values.
- Runtime UI image skin slots are configurable from `project.pompo.json` through `runtimeUiSkin`, allowing project image assets to replace dialogue, name, normal/selected/disabled choice, save, and backlog panels with nine-slice panel rendering and theme-color fallbacks.
- Runtime UI layout geometry is configurable from `project.pompo.json` through `runtimeUiLayout`, covering dialogue/name rectangles, choice dimensions, save menu bounds, save slot spacing, and backlog bounds.
- Runtime UI animation settings are configurable from `project.pompo.json` through `runtimeUiAnimation`, covering panel fade, selected-choice pulse, and typing reveal behavior with validation.
- Runtime playback settings are configurable from `project.pompo.json` through `runtimePlayback`, covering auto-forward delay and skip interval behavior with validation.
- Runtime backlog frame and B-key backlog overlay for previously displayed dialogue lines.
- Runtime choice widgets support disabled options through literal `enabled` flags or bool `enabledVariable` gates; disabled choices render as muted and cannot be selected by keyboard, mouse, or trace playback.
- Runtime save/load overlay frame with a visible quick-save row, six manual slots, stable non-overflowing layout, mouse hover slot selection, guarded click/Enter empty-slot loads, F6 slot save, and guarded F5/F9 quick-save/load when `--saves` is supplied.
- Runtime visual state tracking for background changes, character show/hide/move/expression commands, jump target execution, graph call/return flow with saveable call stacks, and unlocked CG IDs, with FNA placeholder rendering for visible character layers.
- Runtime build-output asset catalog resolution for `Data/project.pompo.json`, copied `Assets/...` files, and character expression sprite lookup, with defensive handling for malformed duplicate catalog keys.
- Runtime audio state tracking for `PlayBgm`, `StopBgm`, `PlaySfx`, `PlayVoice`, and `StopVoice`, including trace/save/restore, build-output audio asset resolution, and guarded FNA `SoundEffect` playback.
- Avalonia editor shell with resizable and collapsible workspace panels, persisted layout presets for balanced, graph-focused, scene-focused, and review workflows, detachable Project, Scene, Graph, Inspector, runtime Preview, and Console windows with a one-click Detach Tools workflow, plus a Help tab for docs and release gates.
- Text JSON project format and required project folders.
- Korean/English `SupportedLocales` and JSON string table contracts for localizable UI text.
- Project schema migration, including schema v1 to v2 runtime UI theme migration, schema v2 to v3 runtime UI skin migration, schema v3 to v4 runtime UI layout migration, schema v4 to v5 runtime UI animation migration, schema v5 to v6 runtime playback migration, schema v6 to v7 disabled-choice skin migration, and future-schema rejection on load/save.
- Project validation diagnostics for missing graph inventory, duplicate scene/character/graph IDs, duplicate/empty character expressions, missing character default expressions, missing jump targets, graph call cycles, duplicate locales, duplicate string keys, missing locale values, unsupported locale values, and graph `textKey` references to missing string tables or keys.
- Project validation diagnostics for invalid runtime UI theme color tokens before build packaging.
- Project validation diagnostics for missing or wrong-type runtime UI skin image asset references before build packaging.
- Project validation diagnostics for runtime UI layout rectangles and dimensions that do not fit the virtual canvas.
- Project validation diagnostics for invalid runtime UI animation timing, text reveal, pulse-strength values, and runtime playback timing values.
- Crash-safe atomic writes for project JSON, graph JSON, build profiles, release manifests, and runtime save slots.
- Asset import, SHA-256 verification, scene, character, graph, runtime save, and build profile models.
- Runtime save slot store with atomic writes, corrupt-file-tolerant listing, load, and delete.
- FNA runtime UI frame model for dialogue, name box, and choice layout.
- VN node catalog with runtime property contracts, default authoring templates, required-property validation, and IR compilation.
- Graph authoring service for node creation with node-specific default properties, movement, connection, property editing, validation, and JSON roundtrip.
- Editor scene authoring surface for scene selection, scene add/delete, project character add/edit/delete with reference guards, character expression sprite assignment with default/reference guards, display name editing, start graph selection, background image assignment, character placement add/edit/delete, visual stage layout review, layer/placement review, and scene save.
- Editor graph ViewModel and UI surface for graph selection, graph add/delete with scene and call-reference guards, reference-safe graph rename, graph loading, visual graph canvas display, canvas node dragging, canvas click-to-connect edge creation, node listing, full VN node palette creation, selected node duplication/deletion, inspector text edits, raw JSON property editing with parse errors, node property hints, undo/redo, dirty state, unsaved-switch guard, diagnostics, and graph save.
- Editor custom script node palette that compiles project `Scripts/**/*.cs`, loads `PompoNodeInput` metadata defaults, and adds custom command/condition nodes to graphs.
- Editor IR preview that compiles the current graph plus sibling project graphs, runs the FNA runtime CLI in an isolated preview process, resolves the selected project locale, and displays lines, choices, variables, audio state, graph call results, and compile diagnostics.
- Editor project workflow actions for minimal/sample template creation, folder open, recent project recall, asset import, validation refresh, doctor readiness checks, build profile creation/editing/deletion with last-profile guard, selectable build profile/platform output with profile summaries, persisted build history shared with CLI automation, build manifest summaries, forced release-candidate build/package, and last-build release packaging with strict verification diagnostics.
- Editor workspace panels are resizable and collapsible across Project, Scene, Inspector, Graph, and Console surfaces, with one-click persisted workspace layout presets for balanced, graph-focused, scene-focused, and review contexts, plus detached Project, Scene, Graph, Inspector, Preview, and Console windows for browsing resources, editing scene data, editing node flow/details, monitoring runtime output, and reviewing diagnostics beside authoring panels. The Workspace toolbar can open the core Project, Graph, Inspector, and Console tools together with `Detach Tools`.
- Searchable editor resource browser with type, broken, and actual-reference unused filters, selected asset details, guarded unused-asset deletion, and visible diagnostics lists.
- Editor localization tab for supported locale add/delete, string table inspection, per-locale string entry add/update/delete with graph-reference guards, localization diagnostics, preview-locale selection, and conservative missing-value repair.
- Editor theme tab for editing runtime UI color tokens, image skin asset IDs, and layout geometry with a live virtual-canvas layout preview, direct drag positioning and resize handles for major UI rectangles, then saving them back to `project.pompo.json`.
- Editor save-slot management tab for listing, selecting, refreshing, and deleting runtime save slots.
- Build manifest generation, standalone build-output verification, and runtime publishing that keep editor assemblies out of runtime output and record supported/smoke-tested locales.
- Build-time `Scripts/**/*.cs` compilation into `Pompo.UserScripts.dll` with project-level, default-deny file system, network, and process permissions, plus manifest inclusion of the generated user script assembly.
- Runtime custom node execution through packaged `Pompo.UserScripts.dll`, with command nodes using the `out` port and condition nodes using `true`/`false` ports.
- Optional packaged-runtime smoke tests that execute compiled IR through the packaged FNA runtime for each supported locale before a build is accepted.
- Reproducible debug and release JSON build profiles under `BuildProfiles/`; release profiles enable self-contained runtime publishing and packaged-runtime smoke tests by default, and project doctor checks every profile file for loadability, safe naming, filename/profileName consistency, required metadata, and configured icon/runtime paths.
- Release zip packaging with SHA-256 checksum and release manifest generation.
- Release manifest verification that checks archive existence, size, checksum files, SHA-256 integrity, build locale audit metadata, required files inside the release archive, and consistency with the embedded build manifest before publishing.
- Release verification requires packaged project data and at least one compiled graph IR.
- Release packaging and verification reject files that are not listed by the build manifest.
- Strict release verification can require packaged-runtime smoke coverage for every supported locale before publishing.
- Strict release verification can require self-contained runtime output and a platform runtime executable before publishing.
- Release packaging and verification reject editor, Avalonia, build-tooling, CLI, and test artifacts in runtime archives.
- Release audit command that combines repository doctor checks, generated documentation-site artifacts, and strict release manifest verification into one pre-publication readiness gate.
- Release archive signing and signature verification with RSA/SHA-256 PEM keys.
- Tag-based GitHub release workflow that builds, tests, packages Windows/macOS/Linux sample runtime artifacts, verifies release manifests, signs when secrets are configured, uploads artifacts, and creates draft GitHub Releases.
- CI and release workflows run localization reports and packaged-runtime smoke tests before publishing artifacts.
- C# extension API, script permission checks, custom node/provider metadata discovery, and runtime module discovery.
- CLI init, version, doctor, validate, profile list/show/save/delete, build, build verify, history list/clear, asset, save, release package/verify/audit/signature commands, and localization report/repair/add-locale/delete-locale commands, with JSON output for automation-facing diagnostics and environment metadata.
- CLI documentation-site generation with `docs site`, producing `index.html`, per-document pages, and a JSON manifest under `artifacts/docs-site`.
- Headless runtime IR execution for smoke testing compiled visual novel graphs, plus interactive `--run-ir` playback that steps one display stop at a time and loads sibling IR files for `CallGraph` targets in packaged build data.
- Runtime `--locale` support for resolving dialogue, narration, speaker, and choice `textKey` values from packaged `Data/project.pompo.json` string tables.
- JSON runtime trace output for automated choice/variable verification.
- Sample VN template with 3 scenes, 2 characters, bundled image/audio assets, character expression metadata, visual background/character/audio commands, choice, branch, variable, save-point, and CG unlock nodes.
- Minimal and sample VN templates seed a default `ui` string table with Korean and English menu labels.

## Open Source Readiness

- License: MIT, see `LICENSE`.
- Changelog: `CHANGELOG.md`.
- Maintainer responsibilities: `MAINTAINERS.md`.
- Getting started and usage guide: `docs/GETTING_STARTED.md`.
- Korean run and usage guide: `docs/RUN_AND_USE.md`.
- Development guide: `docs/DEVELOPMENT.md`.
- Troubleshooting guide: `docs/TROUBLESHOOTING.md`.
- Architecture guide: `docs/ARCHITECTURE.md`.
- Contribution guide: `CONTRIBUTING.md`.
- Code of conduct: `CODE_OF_CONDUCT.md`.
- Support guide: `SUPPORT.md`.
- Security policy: `SECURITY.md`.
- Public release checklist: `docs/OPEN_SOURCE_RELEASE_CHECKLIST.md`.
- Production audit: `docs/PRODUCTION_AUDIT.md`.
- Release roadmap: `docs/ROADMAP.md`.
- Compatibility policy: `docs/COMPATIBILITY.md`.
- C# scripting guide: `docs/SCRIPTING.md`.
- Release process: `docs/RELEASING.md`.
- Local release gate scripts: `scripts/check-release-gates.sh` and `scripts/check-release-gates.ps1`.
- Generated documentation site: `dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json`.
- Documentation hosting workflow: `.github/workflows/docs.yml`.
- Repository hygiene: `.gitignore`, `.gitattributes`, `.editorconfig`, Dependabot, CI workflow, CodeQL workflow, dependency review workflow, bug/feature issue templates, PR template, maintainer responsibilities, and community conduct policy.

## Production Status

PompoEngine is not 1.0 yet. The current codebase is suitable for engine
foundation work and automated validation, but richer authoring workflows and
runtime UI polish remain roadmap items.
