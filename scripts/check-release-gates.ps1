$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepositoryRoot = Resolve-Path (Join-Path $ScriptDirectory "..")

Set-Location $RepositoryRoot

Write-Host "==> Restoring solution"
dotnet restore PompoEngine.slnx

Write-Host "==> Building solution"
dotnet build PompoEngine.slnx --no-restore

Write-Host "==> Running tests"
dotnet test PompoEngine.slnx --no-build

Write-Host "==> Capturing CLI version"
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- version --json

Write-Host "==> Capturing runtime version"
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json

Write-Host "==> Generating documentation site"
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json

Write-Host "==> Running repository doctor"
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .

Write-Host "==> Validating runtime host"
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime

Write-Host "==> Release gates passed"
