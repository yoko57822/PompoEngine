# Support

PompoEngine is pre-1.0 software. The supported path is the documented editor,
CLI, runtime, build, and release workflow in this repository.

## Before Opening an Issue

1. Read `docs/RUN_AND_USE.md` for editor, CLI, build, release, and runtime
   commands.
2. Run the repository or project doctor command that matches the problem:

   ```bash
   dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
   dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project <projectRoot>
   ```

3. For build or release problems, include the build manifest, release manifest,
   and the exact command that failed.

## Good Support Requests

Include:

- Operating system and .NET SDK version.
- PompoEngine commit or release version.
- Whether the issue happens in the editor, CLI, runtime, build output, or user
  scripting.
- Minimal reproduction project or a reduced `project.pompo.json` and graph file.
- Console output, diagnostics, or JSON command output.

## Where to Ask

- Use the bug report template for reproducible defects.
- Use the feature request template for proposed changes.
- Do not open public issues for vulnerabilities. Follow `SECURITY.md` instead.

## Unsupported Requests

The v1 scope excludes mobile, web, console platforms, 3D features, physics
systems, and general-purpose game-engine behavior. Requests in those areas may
be closed or moved to long-term discussion.
