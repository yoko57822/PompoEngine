# Architecture

PompoEngine is a PC-only visual novel engine with a clear separation between
shared data contracts, authoring tools, build tooling, runtime playback, and
advanced user scripting.

## Project Boundaries

- `Pompo.Core` owns shared JSON contracts and data services. It must not depend
  on Avalonia, FNA, build tooling, editor UI, or CLI behavior.
- `Pompo.VisualScripting` owns the VN node catalog, graph validation, traversal,
  authoring helpers, and `PompoGraphIR` compilation.
- `Pompo.Scripting` owns the public C# extension API, user script compilation,
  custom node discovery, runtime module discovery, and script security checks.
- `Pompo.Runtime.Fna` owns runtime playback. It must not reference Avalonia,
  editor code, build tooling, tests, or release packaging code.
- `Pompo.Editor.Avalonia` owns authoring UX. It may use core, visual scripting,
  scripting, and build services, but editor code must not be packaged into
  runtime output.
- `Pompo.Build` owns validation, graph compilation, asset copying, script
  compilation, runtime publishing, build verification, release packaging,
  release verification, release audit, and docs-site generation.
- `Pompo.Cli` is an automation shell around core/build/runtime services.
- `Pompo.Tests` verifies contracts across project files, graph authoring,
  runtime execution, scripting security, build boundaries, release packaging,
  repository doctor, and documentation generation.

## Authoring Data Flow

```text
Editor or CLI
  -> project.pompo.json
  -> Assets / Scenes / Characters / Graphs / Scripts / BuildProfiles / Settings
  -> validation diagnostics
  -> graph compiler
  -> PompoGraphIR
```

All authored project data is text JSON. Binary-only authored formats are out of
scope for v1. Assets are tracked through the asset database with IDs, source
paths, hashes, import options, and references.

## Preview Flow

The editor preview path compiles the current graph plus sibling project graphs
and runs the FNA runtime CLI as an isolated process.

```text
Avalonia editor
  -> graph library compile
  -> runtime preview process
  -> trace events
  -> editor Preview panel/window
```

Preview uses the same IR execution model as packaged runtime smoke tests. This
keeps editor preview close to build output behavior.

## Build Flow

```text
Build profile
  -> project validation
  -> user script compilation
  -> graph IR compilation
  -> asset copy
  -> runtime publish
  -> build manifest
  -> optional packaged-runtime smoke tests
```

Build output must contain only runtime binaries, compiled data, copied assets,
optional `Pompo.UserScripts.dll`, and settings required by the runtime.

## Release Flow

```text
Verified build output
  -> release package
  -> zip archive
  -> checksum
  -> release manifest
  -> optional signature
  -> release verify
  -> release audit
```

Release verification rejects unexpected archive entries, editor/build/test
artifacts, source script files, debug symbols, checksum mismatches, missing
compiled IR, and missing required runtime data. Strict verification can require
self-contained runtime output and smoke-tested locales.

## Runtime Flow

```text
Packaged Data/project.pompo.json
  -> asset catalog
  -> PompoGraphIR
  -> runtime interpreter
  -> FNA presentation
  -> save store
```

The runtime does not interpret source graph JSON directly. It executes compiled
IR and tracks variables, call stacks, visible scene state, BGM/SFX/voice audio
state, unlocked CG IDs, choices, and save slots.

## Scripting Flow

```text
Scripts/**/*.cs
  -> compile-time security checks
  -> Pompo.UserScripts.dll
  -> custom node discovery
  -> editor palette and graph properties
  -> runtime custom node handler
```

The scripting surface is advanced-user functionality. Default script permissions
block file system, network, and process APIs unless explicitly enabled by the
project. Reflection and runtime assembly loading escape hatches remain blocked.

## Runtime UI Customization

Runtime UI customization is project-owned data:

- `runtimeUiTheme`: colors
- `runtimeUiSkin`: image skin slots
- `runtimeUiLayout`: virtual-canvas geometry
- `runtimeUiAnimation`: panel fade, choice pulse timing, and text reveal speed
- `runtimePlayback`: auto-forward delay and skip interval timing

The editor Theme tab exposes these settings with validation and a visual layout
preview. Build validation rejects invalid values before packaging.

## Repository Gates

Before a release candidate:

```bash
dotnet build PompoEngine.slnx --no-restore
dotnet test PompoEngine.slnx --no-build
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
```

Release changes must also pass build verification, release verification, and
release audit with strict locale and self-contained runtime requirements.

## Architectural Rules

- Runtime projects must stay editor-free.
- Core data contracts must stay UI-framework-free.
- Project schema changes require migrations and tests.
- Runtime archive contents must be fully explainable by the build manifest.
- Source scripts must never ship in release archives.
- CLI JSON output used by automation must be treated as a compatibility surface.
- Public docs must change with user-facing editor, runtime, scripting, build, or
  release behavior.
