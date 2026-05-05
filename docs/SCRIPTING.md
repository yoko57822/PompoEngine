# C# Scripting Guide

PompoEngine visual scripting is the default authoring path. C# scripting is an
advanced extension surface for custom VN commands, conditions, runtime modules,
and editor palette metadata.

## File Location

Place user scripts under a project `Scripts/` folder:

```text
MyVN/
  project.pompo.json
  Scripts/
    ReputationGateNode.cs
    AwardCgNode.cs
```

The editor and build pipeline compile `Scripts/**/*.cs` into
`Pompo.UserScripts.dll`. Source `.cs` files are not copied into runtime release
archives.

## Security Defaults

User scripts are compiled with default-deny security gates:

- `System.IO` is blocked unless `scriptPermissions.allowFileSystem` is true.
- `System.Net` is blocked unless `scriptPermissions.allowNetwork` is true.
- `System.Diagnostics` is blocked unless `scriptPermissions.allowProcessExecution`
  is true.
- Reflection, runtime assembly loading, `System.Type.GetType`, and
  `System.Activator.CreateInstance` are always blocked.

Project permissions live in `project.pompo.json`:

```json
{
  "scriptPermissions": {
    "allowFileSystem": false,
    "allowNetwork": false,
    "allowProcessExecution": false
  }
}
```

These checks are a compile-time policy gate for project scripts. They are not an
operating-system sandbox.

## Command Nodes

Use `PompoCommandNode` when the node performs an action and then advances
through the `out` execution port.

```csharp
using Pompo.Scripting;

public sealed class AwardCgNode : PompoCommandNode
{
    [PompoNodeInput("CG ID", DefaultValue = "cg_001")]
    public string CgId { get; set; } = "cg_001";

    public override ValueTask ExecuteAsync(
        PompoRuntimeContext context,
        CancellationToken cancellationToken)
    {
        context.SetVariable("lastUnlockedCg", CgId);
        return ValueTask.CompletedTask;
    }
}
```

## Condition Nodes

Use `PompoConditionNode` when the node chooses between the `true` and `false`
execution ports.

```csharp
using Pompo.Scripting;

public sealed class ReputationGateNode : PompoConditionNode
{
    [PompoNodeInput("Minimum reputation", DefaultValue = 10)]
    public int MinimumReputation { get; set; } = 10;

    public override ValueTask<bool> EvaluateAsync(
        PompoRuntimeContext context,
        CancellationToken cancellationToken)
    {
        var reputation = context.GetVariable<int>("reputation");
        return ValueTask.FromResult(reputation >= MinimumReputation);
    }
}
```

## Runtime Context

`PompoRuntimeContext` exposes runtime variables to custom nodes:

- `SetVariable(string name, object? value)`
- `GetVariable<T>(string name)`
- `Variables`

Custom nodes should treat variable names as stable project contracts. Prefer
simple VN-oriented values: `bool`, `int`, `float`, `string`, enum-like strings,
and asset IDs.

## Editor Palette Metadata

`[PompoNodeInput]` makes a public property visible to the editor custom node
palette and seeds default node properties when the node is added.

```csharp
[PompoNodeInput("Message", DefaultValue = "Hello")]
public string Message { get; set; } = "Hello";
```

The current metadata contract includes:

- `DisplayName`
- `DefaultValue`
- property name
- property type

## Custom Node Providers

Most projects can expose custom nodes by inheriting from `PompoCommandNode` or
`PompoConditionNode` directly. Advanced projects can implement
`ICustomNodeProvider` when they need explicit descriptors.

```csharp
using Pompo.Scripting;

public sealed class MyNodeProvider : ICustomNodeProvider
{
    public IEnumerable<PompoCustomNodeDescriptor> GetNodes()
    {
        yield return new PompoCustomNodeDescriptor(
            "reputation-gate",
            "Reputation Gate",
            typeof(ReputationGateNode),
            Array.Empty<PompoNodeInputDescriptor>());
    }
}
```

## Runtime Modules

`IPompoRuntimeModule` is reserved for runtime setup that should happen when the
packaged user script assembly is loaded.

```csharp
using Pompo.Scripting;

public sealed class StoryRuntimeModule : IPompoRuntimeModule
{
    public void Register(PompoRuntimeContext context)
    {
        context.SetVariable("moduleLoaded", true);
    }
}
```

## Referencing Custom Nodes in Graphs

The editor writes the selected custom node type into graph properties when a
custom node is added from the Graph panel. For hand-authored JSON, the custom
node instruction can identify the script class with `customNodeType`, `type`, or
another compiler-supported node property that resolves to the class name or full
type name.

## Build and Release Behavior

During build:

1. Project validation runs.
2. User scripts compile into `Pompo.UserScripts.dll`.
3. Graphs compile into IR.
4. The runtime package includes compiled data, assets, runtime binaries, and the
   user script assembly when present.
5. Release verification rejects leaked source scripts and editor/build/test
   artifacts.

## Compatibility

The scripting API is pre-1.0 but public. Breaking changes to
`IPompoRuntimeModule`, `ICustomNodeProvider`, `PompoCommandNode`,
`PompoConditionNode`, `PompoRuntimeContext`, `PompoAssetRef<T>`, or
`PompoNodeInput` metadata must be documented in `CHANGELOG.md` and
`docs/COMPATIBILITY.md`.
