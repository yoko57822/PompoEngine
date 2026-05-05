#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/.." && pwd)"

cd "${repo_root}"

echo "==> Restoring solution"
dotnet restore PompoEngine.slnx

echo "==> Building solution"
dotnet build PompoEngine.slnx --no-restore

echo "==> Running tests"
dotnet test PompoEngine.slnx --no-build

echo "==> Capturing CLI version"
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- version --json

echo "==> Capturing runtime version"
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json

echo "==> Generating documentation site"
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json

echo "==> Running repository doctor"
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .

echo "==> Validating runtime host"
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime

echo "==> Release gates passed"
