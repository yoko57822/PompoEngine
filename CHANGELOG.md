# Changelog

All notable PompoEngine changes should be recorded here before a public
release. This project follows a pre-1.0 release model; breaking changes may
still happen, but they must be documented with migration notes when they affect
project files, runtime behavior, build output, or scripting APIs.

## Unreleased

### Added

- PC-only FNA runtime path for visual novel IR playback and packaged build
  smoke testing.
- Avalonia editor shell with project, scene, graph, inspector, preview, theme,
  localization, saves, build, and console surfaces.
- Resizable/collapsible workspace panels, persisted workspace presets, detached
  Project/Scene/Graph/Inspector/Preview/Console windows, workflow focus
  targets, and `Detach Tools`.
- Runtime animation presets for instant, subtle, snappy, and cinematic UI
  timing authoring.
- Runtime UI skin slot for disabled choice widgets through
  `runtimeUiSkin.choiceDisabledBox`.
- JSON project format with schema migrations through runtime UI theme, skin,
  layout, animation, playback, and disabled-choice skin settings.
- VN-specific visual scripting graph authoring, validation, IR compilation, and
  runtime execution.
- Build profiles, release packaging, release verification, release audit,
  signing, and generated documentation-site commands.
- Open-source repository baseline: license, contribution guide, code of
  conduct, support guide, security policy, issue templates, PR template, CI,
  docs publishing workflow, release workflow, and Dependabot.

### Changed

- Runtime UI customization expanded from fixed theme values to project-owned
  theme, image skin, layout, animation, and playback settings.
- Editor workspace workflow expanded from fixed panels to saved presets and
  detachable auxiliary windows with focus actions for project, scene, graph,
  review, and all-panel modes.
- Project schema advanced to v7. Schema v6 projects migrate without data loss;
  disabled choice skins fall back to the normal choice skin when no dedicated
  disabled-choice image is configured.

### Validation

- `dotnet build PompoEngine.slnx --no-restore`
- `dotnet test PompoEngine.slnx --no-build`
- `dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .`
- `dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json`
- `scripts/check-release-gates.sh`
