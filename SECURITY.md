# Security Policy

PompoEngine includes a C# scripting surface for advanced users. Treat scripts as
untrusted project content unless the project explicitly grants additional
permissions.

## Supported Versions

The repository is pre-1.0. Security fixes target `main` until versioned releases
exist.

## Reporting a Vulnerability

Do not publish exploitable details before maintainers have had time to triage.
Open a private advisory or contact the maintainers through the repository owner.

## Current Script Sandbox Rules

By default, user script compilation rejects references to permissioned APIs:

- `System.IO`
- `System.Net`
- `System.Diagnostics`

Projects can opt into those capabilities with `scriptPermissions` in
`project.pompo.json`.

User scripts are always blocked from reflection and runtime assembly loading
surfaces that could bypass those permission checks:

- `System.Reflection`
- `System.Runtime.Loader`
- `System.Type.GetType`
- `System.Activator.CreateInstance`

Treat the scripting surface as a compile-time policy gate for project scripts,
not as a separate OS sandbox.
