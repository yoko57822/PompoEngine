# Compatibility Policy

PompoEngine is pre-1.0. The project can still make breaking changes, but every
breaking change must be explicit, reviewed, tested, and documented before it is
released.

## Versioning

- `0.x` releases may change project schema, runtime behavior, build output, and
  scripting APIs when required to reach a stable engine foundation.
- Every release must update `CHANGELOG.md`.
- A change that affects saved projects, packaged runtime data, public scripting
  types, command-line flags, or release manifest contents must include migration
  or compatibility notes.
- PompoEngine must not claim 1.0 stability until live CI, Pages, and release
  artifact evidence exists for the public repository.

## Project Schema

- `project.pompo.json` uses an explicit `schemaVersion`.
- The loader must reject future schema versions instead of silently accepting
  data it may not understand.
- Every schema bump must include a deterministic migration path from the
  previous schema and regression tests.
- Schema migrations must preserve authored project data whenever possible. When
  data cannot be preserved, the migration notes must call that out.
- Schema v7 adds `runtimeUiSkin.choiceDisabledBox`; schema v6 projects migrate
  without data loss and fall back to `choiceBox` when no disabled-choice image
  skin is configured.
- Build validation must fail on missing references, invalid runtime UI settings,
  invalid runtime playback settings, graph call cycles, unsupported locales, and
  other data that would create ambiguous runtime behavior.

## Visual Scripting and IR

- Runtime builds execute compiled `PompoGraphIR`, not source graph JSON.
- Choice entries may use optional `enabled` boolean literals or
  `enabledVariable` bool variable gates. Older projects without these fields
  remain valid and treat all choices as enabled.
- Newly authored Choice nodes default to `left` and `right` execution outputs
  with matching choice entries. Existing single-output `choice` nodes remain
  valid when their choice entries reference a connected `choice` output.
- Choice entry `port` values must reference connected execution output ports on
  the same Choice node. Broken or disconnected choice ports fail graph
  validation with `GRAPH014` instead of being skipped at runtime.
- A Choice node with only literal `enabled: false` entries fails graph
  validation with `GRAPH015`; use `enabledVariable` for choices that can become
  available through runtime state.
- Choice array entries must be JSON objects. Malformed entries fail graph
  validation with `GRAPH016` instead of crashing validation or disappearing at
  runtime.
- Node property changes that affect runtime execution must update the node
  catalog, graph validation, compiler behavior, preview behavior, and tests
  together.
- New node kinds must document required properties, default authoring values,
  runtime effects, and validation errors.
- IR changes must keep packaged-runtime smoke tests working for supported
  locales.

## Runtime and Build Output

- Runtime output must stay editor-free. Build and release verification must
  reject Avalonia, editor, build-tooling, CLI, test, Roslyn, source script, and
  debug-symbol artifacts in release archives.
- Build manifests and release manifests are part of the release contract. Any
  field change must be documented and covered by verification tests.
- Strict release verification may require self-contained runtime output and
  packaged-runtime smoke coverage for every supported locale.

## C# Scripting API

- Public scripting types should be treated as advanced-user extension points,
  even before 1.0.
- Breaking changes to `IPompoRuntimeModule`, `ICustomNodeProvider`,
  `PompoCommandNode`, `PompoConditionNode`, `PompoRuntimeContext`,
  `PompoAssetRef<T>`, or `PompoNodeInput` metadata must be documented in
  `CHANGELOG.md`.
- Security-sensitive changes must update `SECURITY.md` and tests for blocked or
  explicitly-permitted file system, network, process, reflection, and assembly
  loading surfaces.

## CLI and Automation

- Automation-facing commands should preserve JSON output shape within a release
  line unless a breaking change is documented.
- New release or repository readiness gates must be added to repository doctor,
  release audit, or CI when they protect public publishing quality.
- `docs/RUN_AND_USE.md` must stay current when editor, CLI, runtime, build, or
  release usage changes.
