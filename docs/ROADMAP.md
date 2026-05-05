# Roadmap

This roadmap converts the original implementation plan into release-oriented
milestones.

## 0.1 Foundation

- .NET 10 solution and project boundaries.
- Core JSON project format.
- FNA runtime host.
- Avalonia editor shell.
- Graph validation and IR compilation.
- CLI project creation, validation, build, asset, save, release, and localization report/repair commands.

## 0.2 Playable VN Runtime

- IR interpreter for dialogue, narration, variables, branch, jump, choice, and scene end.
- Text window, choice UI with disabled choice states, configurable typing reveal, backlog, configurable skip and auto-forward timing, per-line interactive playback, and manual save/load.
- Project-configurable runtime UI theme colors.
- Project-configurable runtime UI image skin slots for dialogue, choice, save, and backlog panels.
- Project-configurable runtime UI layout geometry for dialogue, choice, save, and backlog surfaces.
- Project-configurable runtime UI animation settings for panel fade, selected-choice pulse, and text reveal speed.
- Project-configurable runtime playback settings for auto-forward delay and skip interval.
- Korean/English string table contracts, graph `textKey` validation, and runtime locale selection for localizable UI text.
- Sample VN project with scenes, characters, branches, and audio placeholders.

## 0.3 Editor Authoring

- Resource browser with search, broken references, and unused asset filters.
- Scene authoring for scene selection, scene add/delete, project character add/edit/delete with reference guards, character expression sprite assignment with default/reference guards, background assignment, start graph selection, character placement add/edit/delete, visual stage layout review, layer/placement review, and dialogue UI preview.
- Node graph editor with creation, duplication, deletion, linking, drag/move, inspector editing, undo/redo, and validation.
- Persisted workspace layout presets, collapsible panel visibility controls, and detached Project/Scene/Graph/Inspector/Preview/Console window support for balanced, graph-focused, scene-focused, and review-oriented editor work.
- Localization tab with locale review, string table diagnostics, preview locale selection, and missing-value repair.
- Preview process protocol with selected locale, current node, variable state, choice history, and call stack.
- Build panel profile/platform selection plus build manifest summaries and release-candidate build/package flow with forced runtime packaging, smoke-tested locales, and strict verification diagnostics.

## 0.4 Build and Packaging

- Windows, macOS, and Linux build profiles.
- Runtime publishing and asset copying.
- Build log diagnostics that point to graph, scene, node, and asset IDs.
- Smoke run of packaged runtime through compiled IR for each supported locale.
- Build manifests that record supported locales and smoke-tested locales for release audit.
- Release packaging, archive-content verification, signing, artifact upload, and draft GitHub Release creation.
- Runtime audio channels for BGM, SFX, and voice playback state.

## 0.5 Extension API

- User script assembly compilation and build manifest inclusion.
- Custom node/provider discovery and editor input metadata.
- Editor custom script node palette.
- Runtime custom command and condition node execution.
- Runtime module discovery.
- Project-level explicit permission settings for file system, network, and process execution.

## 1.0 Production Release

- Stable project schema and migration path, including exercised v1 to v7 migration coverage.
- Crash-safe writes and undo/redo.
- Release packaging and checksums.
- Generated static documentation site and GitHub Pages deployment workflow.
- Automated sample project build and run verification for every release.

Remaining blockers before calling the engine 1.0 stable:

- Extend runtime UI skinning with richer widget states and editor-side visual animation authoring beyond the current preset-based timing controls and live layout preview.
- Polish the editor into a designer-grade detachable docking experience beyond the current resizable/collapsible panels, workflow presets, and detached Project/Scene/Graph/Inspector/Preview/Console windows.
- Exercise every future project schema change with a dedicated migration and regression test.
- Collect live public Pages deployment evidence for the generated documentation site.
- Collect live public CI evidence for Windows, macOS, and Linux release artifacts.
