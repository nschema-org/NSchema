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
**lowest to highest precedence**: the loaded **`nschema.json`**, then **environment variables**, then **command-line
options**. The default `./nschema.json` is optional; an explicit `--config` must exist.

All three are unified in one mechanism: the single `_overrides` table of `ConfigOverride` entries. Each entry names a
command-line `Option`, an optional environment variable (from the `EnvironmentVariables` constants — never raw strings),
and an apply delegate that writes the resolved value onto the config (creating nested provider/state sections on demand).
`ConfigOverride.Apply` enforces precedence per setting: an explicitly-set CLI option (`{ Implicit: false }`) wins over the
environment variable, which wins over the file value — an unspecified flag never clobbers a config/env value. There is no
separate env list, no per-section routing method, and no string-parsing shorthand: `--state-s3-bucket`/`--state-s3-key`
(and their env vars) populate `state.s3.bucket`/`state.s3.key` directly. Environment lookups are an allow-list, not a
blanket prefix — only the variables named in an override are honored.

This project deliberately does **not** use `Microsoft.Extensions.Configuration` — config is plain STJ so the loader and
`init`'s writer share one format (`JsonOptions`) and one set of attributes (`[JsonPropertyName]`). Don't reintroduce the
config binder. `nschema.json` keys are camelCase; `schema.dir` maps to `SchemaConfig.Directory` via `[JsonPropertyName]`.

## From config to a run

`CliApplicationBuilder` is a fluent wrapper around the core `NSchemaApplicationBuilder`. Each command opts into only the
configuration slices it needs (`ConfigureDesiredSchema`, `ConfigureScope`, `ConfigurePolicies`, `ConfigureDatabaseProvider`,
`ConfigureBackendState`, `ConfigureConfirmation`) — there is no shared "configure everything" method, so adding a slice to
one command does not affect another. `refresh` intentionally omits the desired-schema and scope slices (it snapshots the
**whole** live schema). `ConfigureDatabaseProvider`/`ConfigureBackendState` dispatch on **which nested section is
populated** (`ProviderConfig.Postgres`, `StateConfig.File`/`S3`) rather than on a discriminator enum — each section is a
strongly-typed bag of that provider's settings (connection string, timeouts, …). No populated section is valid and means
offline (plan/refresh against a state store only). Validation uses **FluentValidation** (`ProviderConfigValidator`,
`StateConfigValidator`, and per-section leaf validators) — the run path runs the relevant validator and throws an
`InvalidOperationException` with the joined `ErrorMessage`s before building, and a future `validate` command can reuse the
validators directly. The "at most one section" rule is checked via each config's `ConfiguredSectionCount`. After
validation, the builder binds the now-guaranteed values through **property patterns** (`{ Postgres: { ConnectionString:
{ } cs } }`), so there are no null-forgiving operators when reading provider/state config.

Each nested section is also reachable from the flat CLI/env overrides in the `_overrides` table: `--connection-string`
populates `provider.postgres.connectionString`, `--state-file` populates `state.file.path`, and
`--state-s3-bucket`/`--state-s3-key` populate `state.s3.bucket`/`state.s3.key` (each creating its section on demand). A
bare `--provider postgres` just ensures the section exists. Rich settings (e.g. `commandTimeout`) are file-only.

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
