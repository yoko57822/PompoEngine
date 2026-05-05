## Summary

## Validation

- [ ] `dotnet build PompoEngine.slnx`
- [ ] `dotnet test PompoEngine.slnx`
- [ ] `dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .`
- [ ] `dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json`
- [ ] `pompo doctor --project <sample-or-test-project>` if project/release readiness changed
- [ ] CLI smoke tested if project/build behavior changed
- [ ] Runtime smoke tested if graph/IR/runtime behavior changed
- [ ] Release audit run if release packaging, CI, docs publishing, or distribution files changed

## Notes
