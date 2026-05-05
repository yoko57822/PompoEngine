# Contributing

PompoEngine is designed as a PC-only visual novel engine with an Avalonia editor,
an FNA runtime, a JSON-first project format, and VN-specific visual scripting.

## Development Setup

1. Install .NET SDK `10.0.100`.
2. Read `docs/DEVELOPMENT.md`.
3. Run `scripts/check-release-gates.sh` or `pwsh scripts/check-release-gates.ps1`.

## Contribution Rules

- Follow the project `CODE_OF_CONDUCT.md` in issues, pull requests, reviews,
  and release discussions.
- Keep `Pompo.Runtime.Fna` free of Avalonia, editor, and build-tool references.
- Keep `Pompo.Core` free of UI framework dependencies.
- Store authored project data as text JSON.
- Add tests for graph validation, build packaging, project migration, and runtime execution changes.
- Keep CLI and editor behavior backed by the same core services.
- Keep the development guide current when local commands, project boundaries,
  or release gates change.

## Pull Request Checklist

- [ ] `scripts/check-release-gates.sh` or `scripts/check-release-gates.ps1` passes.
- [ ] Runtime output does not include editor assemblies unless intentionally testing editor tooling.
- [ ] Public project format changes include migration or compatibility notes.
- [ ] New user-facing behavior is documented in `README.md` or `docs/`.
