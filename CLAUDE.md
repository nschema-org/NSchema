# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`NSchema.Cli` is a thin command-line front-end (packaged as the `nschema` .NET global tool) over the **NSchema** core
framework — a declarative database schema migration engine ("Terraform for database schemas"). The CLI's job is to
resolve configuration, translate it into a core `NSchemaApplication`, and run one operation.

The core and providers (`NSchema`, `NSchema.Postgres`, `NSchema.Aws`, `NSchema.Yaml`) are consumed as **NuGet
packages**, not project references (versions pinned in `Directory.Packages.props`). Their source lives in sibling repos
under `../` (e.g. `../NSchema`). Changing core behavior therefore requires publishing a new core package and bumping the
pinned version here — it cannot be done from this repo alone.

## Commands

```sh
dotnet build NSchema.Cli.slnx
dotnet test  NSchema.Cli.slnx                                   # all tests
dotnet test  NSchema.Cli.slnx --filter "FullyQualifiedName~NSchemaConfigurationFactoryTests"   # one class
dotnet test  NSchema.Cli.slnx --filter "FullyQualifiedName~RootCommandTests.HasTheNschemaCommandName"  # one test
```

- **`dotnet test` requires a running Docker daemon** — `MigrationRoundTripTests` spins up a PostgreSQL container via
  Testcontainers. The other tests don't need it, but there's a single test project, so a Docker-less run will fail those.
- `TreatWarningsAsErrors` and `GenerateDocumentationFile` are on — builds fail on warnings, and the build packs the tool
  (`GeneratePackageOnBuild`). Target framework is `net10.0`.

## Configuration resolution (the heart of the CLI)

`Configuration/NSchemaConfigurationFactory.Create(ParseResult)` produces an `NSchemaConfiguration` from three layers,
**lowest to highest precedence**:

1. **`nschema.json`** — deserialized with System.Text.Json (`JsonOptions`). `--config` overrides the path; the default
   `./nschema.json` is optional, an explicit `--config` must exist.
2. **Environment variables** — an explicit allow-list (`_environmentOverrides`), e.g. `NSCHEMA_CONNECTION_STRING`,
   `NSCHEMA_PROVIDER`. Not a blanket prefix — only listed variables are honored.
3. **Command-line options** — `_cliOverrides`, applied only when the option was explicitly set (`{ Implicit: false }`),
   so an unspecified flag never clobbers a config/env value.

This project deliberately does **not** use `Microsoft.Extensions.Configuration` — config is plain STJ so the loader and
`init`'s writer share one format (`JsonOptions`) and one set of attributes (`[JsonPropertyName]`). Don't reintroduce the
config binder. `nschema.json` keys are camelCase; `schema.dir` maps to `SchemaConfig.Directory` via `[JsonPropertyName]`.

## From config to a run

`CliApplicationBuilder` is a fluent wrapper around the core `NSchemaApplicationBuilder`. Each command opts into only the
configuration slices it needs (`ConfigureDesiredSchema`, `ConfigureScope`, `ConfigurePolicies`, `ConfigureDatabaseProvider`,
`ConfigureBackendState`, `ConfigureConfirmation`) — there is no shared "configure everything" method, so adding a slice to
one command does not affect another. `refresh` intentionally omits the desired-schema and scope slices (it snapshots the
**whole** live schema). `ConfigureDatabaseProvider`/`ConfigureBackendState` dispatch on the `ProviderType`/`StateType`
enums; a `null` provider is valid and means offline (plan/refresh against a state store only).

## Error handling and output

The CLI is the **single** presenter of errors. `Program.cs` disables System.CommandLine's default exception handler
(`EnableDefaultExceptionHandler = false`) and maps exceptions to exit codes (130 for cancellation, 1 otherwise). The core
is configured with `WithExceptionBehavior(ExceptionBehavior.Throw)` so it does **not** also print failures. Run output
(plan diffs, progress) flows through the core's `IMigrationReporter`; avoid direct `Console` writes except in `Program.cs`
(top-level errors) and the interactive prompt in `ConsoleMigrationConfirmation`.

## Options layout

`Configuration/CliOptions.cs` groups options by applicability: `Global` (recursive, all commands), `Database`/`State`
(recursive), `Desired`/`Migration` (registered per-command on plan/apply), `Apply` (apply only), `Init` (init only).
`RootCommand.Create` wires these onto commands. Note `RootCommand` sets the displayed command name to `nschema` via
reflection on a System.CommandLine internal (it otherwise derives "NSchema.Cli" from the assembly, which can't be renamed
without colliding with the core `NSchema` assembly); `RootCommandTests.HasTheNschemaCommandName` guards that reflection.

## Desired-schema files

The files users point `--schema-dir` at are **`DatabaseSchema` documents** (YAML by default, or JSON). Column `type` is a
**compact string** (`bigint`, `text`, `varchar(255)`), not the `{ "kind": ... }` object form — that object form is the
state-snapshot serialization, which is a different thing. See `README.md` for a worked example.

## Test conventions

Tests use `// Arrange` / `// Act` / `// Assert` sections and a single member-level `_sut` field where the system under
test is an instance (static types like `NSchemaConfigurationFactory` use a small invocation helper instead). Mocks use
NSubstitute, assertions use Shouldly. Test parallelization is disabled assembly-wide because config resolution reads
process-global environment variables; `NSchemaConfigurationFactoryTests` snapshots and clears `NSCHEMA_*` in its
constructor to stay hermetic.
