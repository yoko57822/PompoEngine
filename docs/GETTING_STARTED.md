# Getting Started

This guide explains how to run PompoEngine locally, create a visual novel
project, preview it, and produce a standalone release candidate.

## Prerequisites

- Install .NET SDK `10.0.100`.
- Use a desktop OS: Windows, macOS, or Linux.
- Run commands from the PompoEngine repository root unless noted otherwise.

Verify the SDK and repository:

```bash
dotnet --version
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx --no-restore
dotnet test PompoEngine.slnx --no-build
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- version --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
```

## Run the Editor

Start the Avalonia editor:

```bash
dotnet run --project src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj
```

In the editor:

1. Choose `New Sample` to create a sample visual novel project, or `Open` to
   open an existing project folder.
2. Use `Dashboard` to review project counts and production readiness checks.
3. Use `Workspace` to browse assets, add or delete scenes, edit project
   characters and expression sprites, edit scene fields, review the visual stage
   layout, author graph nodes, duplicate/delete/connect selected nodes, inspect
   graph properties, and run graph previews.
4. Use the Project resource browser's broken/unused filters before deleting
   assets. Referenced assets are protected from deletion.
5. Use `Localization` to inspect Korean/English string tables and fill missing
   values conservatively.
6. Use `Saves` to list or delete runtime save slots for the loaded project.
7. Use `Build` to select a build profile, choose the target platform, build the
   project, and package a verified release candidate.

Run `Run Doctor` on the Dashboard before treating a project as release-ready.

## Create a Project from the CLI

Create a sample project:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/MyVN --name MyVN --template sample --json
```

Create a smaller starter project:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/MyVN --name MyVN --template minimal --json
```

The project root contains:

- `project.pompo.json`
- `Assets/Images`, `Assets/Audio`, `Assets/Fonts`
- `Scenes`, `Characters`, `Graphs`, `Scripts`
- `BuildProfiles`, `Settings`

## Validate Project Health

Run the project doctor:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/MyVN --json
```

Run validation directly:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN --json
```

Check localization coverage:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization report --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization report --project /tmp/MyVN --json
```

Add or delete a supported locale:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization add-locale --project /tmp/MyVN --locale ja
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization add-locale --project /tmp/MyVN --locale ja --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization delete-locale --project /tmp/MyVN --locale ja
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization delete-locale --project /tmp/MyVN --locale ja --json
```

Fill missing localization values from a fallback locale:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization repair --project /tmp/MyVN --fallback-locale ko
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization repair --project /tmp/MyVN --fallback-locale ko --json
```

## Import and Verify Assets

List project assets:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project /tmp/MyVN --type Image --json
```

Import an image asset:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset import --project /tmp/MyVN --file ./background.png --type Image --asset-id bg-main --json
```

Asset IDs are also used as project asset file names, so they may contain only
letters, numbers, `-`, `_`, and `.`, and must not start or end with `.`.
Graph IDs follow the same safe file-name rule because builds write
`<graphId>.pompo-ir.json` files.

Verify asset hashes:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN --json
```

Delete an unused asset:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset delete --project /tmp/MyVN --asset-id unused-bg --json
```

Referenced assets are rejected. Add `--keep-file` if the asset database entry
should be removed while leaving the copied file on disk.

If an asset was intentionally replaced outside the editor, refresh hashes:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset rehash --project /tmp/MyVN --json
```

## Build a Standalone Runtime

List or create build profiles from CLI automation:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile save --project /tmp/MyVN --name demo --platform MacOS --app-name MyVN --version 0.1.0 --data-only --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile show --project /tmp/MyVN --name demo
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile delete --project /tmp/MyVN --name demo --json
```

Profile names are used as build-output folder names, so they may contain only
letters, numbers, `-`, `_`, and `.`, and must not start or end with `.`.

`doctor --project` validates every `BuildProfiles/*.pompo-build.json` file, not
only the selected build profile. It also checks profile metadata and configured
icon/runtime paths before a build starts.
Project validation also rejects duplicate or empty character expression IDs and
default expressions that do not exist.

Use the release build profile:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --output /tmp/MyVN/Builds
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --output /tmp/MyVN/Builds --json
```

Override the target platform when needed:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --platform Linux --output /tmp/MyVN/Builds
```

The build output contains runtime files, copied assets, compiled graph IR, and
`pompo-build-manifest.json`. If the project has `Scripts/**/*.cs`, the build
compiles them into `Pompo.UserScripts.dll` and lists that assembly in the build
manifest; script source files are not copied. By default, scripts that reference
file system, network, or process APIs fail the build until explicit permission
settings are added to `project.pompo.json`:

```json
{
  "scriptPermissions": {
    "allowFileSystem": false,
    "allowNetwork": false,
    "allowProcessExecution": false
  }
}
```

Reflection and runtime assembly loading APIs such as `System.Reflection`,
`System.Runtime.Loader`, `System.Type.GetType`, and
`System.Activator.CreateInstance` are blocked even when those permissions are
enabled.

Build output must not contain editor assemblies.

Custom visual graph nodes use `GraphNodeKind.Custom` with a `nodeType`,
`customNodeType`, or `type` property that matches a user script class name or
full type name. `PompoCommandNode` implementations advance through the `out`
execution port. `PompoConditionNode` implementations advance through `true` or
`false` execution ports.
In the editor, the Graph panel compiles project scripts and exposes discovered
custom nodes in the custom node picker. `[PompoNodeInput]` property metadata is
used to seed default node properties when a custom node is added.

Verify the build output before packaging:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/MyVN/Builds/MacOS/release --require-smoke-tested-locales --require-self-contained
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/MyVN/Builds/MacOS/release --json
```

Build history is written to `Settings/build-history.pompo.json` and can be
checked from automation:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history list --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history clear --project /tmp/MyVN --json
```

## Run or Smoke-Test the Runtime

Validate the local FNA runtime host:

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime
```

Run a compiled graph headlessly:

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --choices 1 --json-trace
```

Run the interactive FNA playback path:

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --run-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --locale ko --saves /tmp/MyVN/Saves
```

Runtime controls:

- `Space` or left click: complete the current typing reveal, then advance text.
- Mouse hover or `Up`/`Down`: move choice selection.
- `Enter`: complete the current typing reveal, then confirm a choice.
- `A`: toggle auto-forward.
- `S`: toggle skip.
- `B`: show backlog.
- `F5`: quick save when `--saves` is supplied.
- Mouse hover over manual save slots: move save-slot selection.
- Left click a save slot: load that slot when it has data.
- `F9`: quick load when `--saves` is supplied.

## Package and Verify a Release

Package the build output:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release package --build /tmp/MyVN/Builds/MacOS/release --output /tmp/MyVN/Releases --name MyVN-0.1.0-macos --json
```

Verify the release manifest strictly:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```

Optional signing:

```bash
openssl genrsa -out release-private.pem 4096
openssl rsa -in release-private.pem -pubout -out release-public.pem
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release sign --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --private-key ./release-private.pem --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify-signature --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --public-key ./release-public.pem --json
```

## Save Slot Tools

List runtime saves:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save list --saves /tmp/MyVN/Saves
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save list --saves /tmp/MyVN/Saves --json
```

Delete a slot:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save delete --saves /tmp/MyVN/Saves --slot quick
```
