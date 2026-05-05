# Releasing PompoEngine

PompoEngine releases are built by `.github/workflows/release.yml`.

## Tag Release

1. Create a tag named `vX.Y.Z`.
2. Push the tag to GitHub.
3. The release workflow builds, tests, packages, verifies, signs when keys are configured, uploads artifacts, and creates a draft GitHub Release.

Build profiles can set `runSmokeTest` to `true` to require the packaged FNA
runtime to execute every compiled IR graph headlessly for each supported
locale before the build is accepted.
New Pompo projects include both `BuildProfiles/debug.pompo-build.json` and
`BuildProfiles/release.pompo-build.json`; the release profile enables packaged
runtime smoke tests and self-contained runtime publishing by default. Debug
profiles stay framework-dependent for faster local iteration.

Each `pompo-build-manifest.json` records the project's supported locales and
the exact locale set that completed packaged-runtime smoke testing. A release
artifact with `runSmokeTest` disabled will still list supported locales, but
its `smokeTestedLocales` list will be empty.
The packaged FNA runtime loads sibling `*.pompo-ir.json` files from the build
`Data/` directory so `CallGraph` and `Return` flows execute through the same
compiled IR set that release verification packages.
Release packaging requires a valid build manifest and copies that locale audit
summary into the release manifest for later verification. Package verification
also opens the release archive and checks that `pompo-build-manifest.json` and
every build-manifest `includedFiles` entry are present in the zip. The release
manifest's build metadata must also match the embedded build manifest in the
archive.
Release artifacts must include `Data/project.pompo.json` and at least one
compiled graph IR.
Release package names are used as archive, checksum, and manifest file names;
they may contain only letters, numbers, `-`, `_`, and `.`, and must not start
or end with `.`.
Files not listed by `pompo-build-manifest.json` are rejected during packaging
and release verification.
Packaging and verification reject forbidden runtime artifacts such as
`Pompo.Editor.*`, `Avalonia*`, `Pompo.Build*`, `Pompo.Cli*`, `Pompo.Tests*`,
`Microsoft.CodeAnalysis*`, `xunit*`, and `testhost*` files, so runtime archives
remain separated from the Avalonia editor, Roslyn compiler, build tooling, and
test assemblies. Release runtime packaging also removes and rejects `.pdb`
debug symbol files.

The release workflow also runs `pompo localization report --json` on the sample
project, `pompo doctor --repository` for repository readiness, `pompo doctor`
for project/release-profile readiness, `pompo build verify` for build-output
manifest integrity, and
`pompo build --run-smoke-test` so localization quality, authoring health, and
packaged runtime execution are checked before artifacts are uploaded.
Localization report, build, package, release verification, signing, and
signature verification commands emit JSON in CI so release logs keep
machine-readable diagnostics.
It verifies the resulting release manifest with
`--require-smoke-tested-locales`, which fails if any supported locale lacks
packaged-runtime smoke coverage, and `--require-self-contained`, which fails
if the release artifact requires an installed .NET runtime or omits the
platform runtime executable.
Before publishing a release candidate manually, generate the documentation site
and run `pompo release audit --root . --manifest <releaseManifestJson>
--require-smoke-tested-locales --require-self-contained --json`. The audit
combines repository doctor diagnostics, generated documentation-site artifact
checks, and strict release manifest verification into one readiness result.

Project validation also checks authoring contracts before packaging: a project
must define at least one graph, scene/character/graph IDs must be unique,
graph calls must not contain cycles, supported locales must be unique, and
every string table entry must provide values for the project's supported
locales without unsupported locale keys. Graph dialogue, narration, and choice
nodes that use `textKey` must reference existing string tables and keys, and
jump nodes must target existing nodes. Graph validation also rejects nodes that
omit required runtime properties from the node catalog, including empty asset,
character, graph, jump-target, variable, and line-text fields unless a supported
alias such as `textKey`, `targetGraphId`, or `backgroundAssetId` supplies the
value. Runtime UI theme colors must use `#RRGGBB` or `#RRGGBBAA`; invalid theme
tokens fail validation before release packaging.

## Signing Keys

Configure these repository secrets to sign release archives:

- `POMPO_RELEASE_PRIVATE_KEY_PEM`
- `POMPO_RELEASE_PUBLIC_KEY_PEM`

Generate a key pair with:

```bash
openssl genrsa -out release-private.pem 4096
openssl rsa -in release-private.pem -pubout -out release-public.pem
```

The workflow never uploads the private key. It uploads `.zip`, `.zip.sha256`, optional `.zip.sig`, and `.release.json` artifacts.
