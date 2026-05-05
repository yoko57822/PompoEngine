# Troubleshooting

Use this guide when PompoEngine fails to load a project, preview a graph, build
runtime output, verify a release, or run repository gates.

## First Checks

Run these from the repository root:

```bash
dotnet --version
scripts/check-release-gates.sh
```

PowerShell equivalent:

```powershell
pwsh scripts/check-release-gates.ps1
```

If the gate script fails, rerun the failing section manually:

```bash
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx --no-restore
dotnet test PompoEngine.slnx --no-build
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
```

If repository doctor reports `REPO011`, restore the Unix executable bit:

```bash
chmod +x scripts/check-release-gates.sh
```

The expected SDK is `10.0.100`. `global.json` disables roll-forward, so an
unexpected SDK version should be fixed before diagnosing engine behavior.

## Project Does Not Open

Run:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project <projectRoot>
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project <projectRoot> --json
```

Check for:

- Missing `project.pompo.json`
- Future unsupported `schemaVersion`
- Duplicate scene, character, graph, locale, or string keys
- Missing graph inventory
- Broken asset references
- Invalid runtime UI theme, skin, layout, or animation settings

## Assets Are Missing or Marked Broken

Run:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project <projectRoot> --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project <projectRoot> --json
```

If a source file changed intentionally, reimport or rehash the asset through the
editor or CLI. If an asset is unused, verify that it is not referenced by scenes,
characters, graphs, runtime UI skin slots, or localization before deletion.

## Graph Preview Fails

In the editor:

1. Save the current graph.
2. Check the Graph diagnostics column.
3. Confirm the selected preview locale exists in the project.
4. Run Preview again.

From CLI/runtime paths, build the project and execute compiled IR:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project <projectRoot> --profile-file <profileFile> --output <buildRoot>
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir <buildOutput>/Data/graph_intro.pompo-ir.json --json-trace
```

Common causes:

- Missing `Start` node
- Broken jump or `CallGraph` target
- Type mismatch in node properties
- Graph call cycle
- Missing `textKey` string table entry
- Custom node compile failure

## User Scripts Fail to Compile

Run a project build or use the editor custom node palette status. Script
diagnostics usually include the source path and line.

Common causes:

- Referencing blocked namespaces such as `System.IO`, `System.Net`, or
  `System.Diagnostics` without explicit `scriptPermissions`
- Using reflection or runtime assembly loading APIs that are always blocked
- Missing `using Pompo.Scripting;`
- Custom node type names that do not match graph `customNodeType` values

See `docs/SCRIPTING.md` for examples and security rules.

## Build Fails

Run:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project <projectRoot> --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project <projectRoot> --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project <projectRoot> --profile-file <profileFile> --output <buildRoot>
```

Check:

- Build profile filename and `profileName` consistency
- Required app name, version, platform, and runtime packaging settings
- Invalid icon or runtime path
- Graph compile diagnostics
- Script compile diagnostics
- Runtime UI validation diagnostics

## Release Verification Fails

Run:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build <buildOutput> --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify --manifest <releaseManifestJson> --require-smoke-tested-locales --require-self-contained --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest <releaseManifestJson> --require-smoke-tested-locales --require-self-contained --json
```

Common causes:

- Missing runtime executable
- Missing compiled graph IR
- Missing smoke-tested locale coverage
- Archive checksum mismatch
- Release archive contains editor, build, CLI, test, Roslyn, source script, or
  debug-symbol artifacts
- Archive entries not listed by the build manifest
- Generated docs site missing
- Repository doctor failure

## Documentation Site Fails

Run:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
```

The docs generator treats required Markdown files as release artifacts. If a
required doc is missing or renamed, update `DocumentationSiteService`,
repository doctor, and tests in the same change.

## CI Fails But Local Build Passes

Check:

- The CI runner is using `global.json`.
- Workflow permissions are still least-privilege.
- Generated docs-site output is not committed.
- Release workflows are running on tag or `workflow_dispatch` as expected.
- Dependency Review or CodeQL failed on a dependency/security issue rather than
  a compile error.

## What To Include In A Bug Report

- OS and .NET SDK version
- PompoEngine commit or release version
- Exact command or editor action
- Project doctor or repository doctor output
- Build manifest or release manifest when packaging is involved
- Minimal graph, scene, asset, or script file needed to reproduce
