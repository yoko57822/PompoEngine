# Maintainers

PompoEngine is pre-1.0. Until the public repository owner and maintainer teams
are finalized, maintainership is defined by responsibility area rather than
GitHub handles.

## Responsibility Areas

- Runtime: `src/Pompo.Runtime.Fna`, runtime save/load, rendering, audio,
  localization playback, and packaged runtime smoke behavior.
- Editor: `src/Pompo.Editor.Avalonia`, editor workflow, workspace layout,
  detached tool windows, ViewModels, and authoring UI.
- Core data model: `src/Pompo.Core`, project schema, migrations, assets, scenes,
  characters, saves, and shared runtime contracts.
- Visual scripting: `src/Pompo.VisualScripting`, node catalog, validation,
  graph authoring, IR compilation, and graph compatibility.
- Scripting security: `src/Pompo.Scripting`, user script compilation,
  permission gates, public scripting API, and custom node loading.
- Build and release: `src/Pompo.Build`, `src/Pompo.Cli`, build profiles,
  manifests, release packaging, signing, release audit, CI, and documentation
  site generation.
- Documentation and community: `README.md`, `docs/`, `CHANGELOG.md`,
  `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SUPPORT.md`, `SECURITY.md`, and
  `.github/`.

## Review Expectations

- Runtime output must not gain editor, Avalonia, build-tooling, CLI, test,
  Roslyn, source script, or debug-symbol artifacts.
- Project schema changes must include migration behavior and regression tests.
- Public scripting API changes must document compatibility and security impact.
- Release, CI, and docs workflow changes must keep `release audit`,
  repository doctor, and docs-site generation passing.
- User-facing editor, CLI, runtime, or build behavior changes must update
  `README.md`, `docs/RUN_AND_USE.md`, or release documentation as appropriate.

## Before Public 1.0

When repository ownership is finalized, add a real `.github/CODEOWNERS` file
with valid GitHub users or teams for the responsibility areas above. Do not use
placeholder owners in CODEOWNERS because GitHub treats unresolved owners as an
invalid review rule.
