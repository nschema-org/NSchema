# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`NSchema` is a thin command-line front-end (packaged as the `nschema` .NET global tool) over the **NSchema** core
framework — a declarative database schema migration engine ("Terraform for database schemas"). The CLI's job is to
resolve configuration, translate it into a core `NSchemaApplication`, and run one operation.

The core and providers (`NSchema`, `NSchema.Postgres`, `NSchema.Aws`, `NSchema.Yaml`) are consumed as **NuGet
packages**, not project references (versions pinned in `Directory.Packages.props`). Their source lives in sibling repos
under `../` (e.g. `../NSchema`). Changing core behavior therefore requires publishing a new core package and bumping the
pinned version here — it cannot be done from this repo alone.

## Commands

```sh
dotnet build NSchema.slnx
dotnet test  NSchema.slnx                                   # all tests
dotnet test  NSchema.slnx --filter "FullyQualifiedName~OptionBindingTests"   # one class
dotnet test  NSchema.slnx --filter "FullyQualifiedName~RootCommandTests.HasTheNschemaCommandName"  # one test
```

- **`dotnet test` requires a running Docker daemon** — `MigrationRoundTripTests` spins up a PostgreSQL container via
  Testcontainers. The other tests don't need it, but there's a single test project, so a Docker-less run will fail those.
- `TreatWarningsAsErrors` and `GenerateDocumentationFile` are on — builds fail on warnings, and the build packs the tool
  (`GeneratePackageOnBuild`). Target framework is `net10.0`.

## Configuration resolution (the heart of the CLI)

A command's config is resolved in three layers, **lowest to highest precedence**: the loaded **`nschema.json`**, then
**environment variables**, then **command-line options**. `ConfigurationFactory.Load<T>(ParseResult)` drives it — it
deserializes the file into `T` (if any; the default `./nschema.json` is optional, an explicit `--config` must exist), then
calls `T.Bind(ParseResult)` to layer env + CLI over it. `T` is the command's own `IBindable` config model (see below); the
factory is generic and knows nothing about commands or slices.

The unit of resolution is **`Configuration/Binding/OptionBinding<T>`** — one object owning a single binding: an optional
System.CommandLine `Option<T>`, an optional environment variable, and a parser. It is built fluently
(`OptionBinding.Create<T>().FromOption("--x").FromEnvironmentVariable(EnvVar).WithDescription(...)`); `.AllowMultipleArguments()`
and `.Recursive()` configure the underlying option, which is built lazily and cached on first `.Option` access. At least
one source is required: a binding with only `.FromEnvironmentVariable` (no `.FromOption`) is **environment-only** — it
registers no CLI option and `.Option` throws (e.g. the connection string, `NSCHEMA_POSTGRES_CONNECTION_STRING`); one with
only `.FromOption` is CLI-only. **Parsing the env string is automatic**: enums via case-insensitive `Enum.Parse`, strings
via identity; pass a parser to `.FromEnvironmentVariable(env, parser)` only for other types (an unparseable env value with
no parser throws).

`OptionBinding.Bind(result, apply)` enforces the precedence per binding: an explicitly-set CLI option (`{ Implicit: false }`)
wins over the environment variable, which wins over whatever value is already on the model (the file/base value) — when
neither CLI nor env is set, `apply` is **not** called, so an unspecified flag never clobbers a config value.
(`TryGetValue`/`GetValueOrDefault` expose the same resolution without a setter, for consumers like `ConfigurationFactory`
reading `--config` and `ConsoleFactory`/`Program` reading `--no-color`.) Environment lookups are an allow-list: a binding
reads only the one variable it was given, named from the `EnvironmentVariables` constants — never raw strings.

This project deliberately does **not** use `Microsoft.Extensions.Configuration` — config is plain STJ so the loader and
`init`'s writer share one format (`JsonOptions`) and one set of attributes (`[JsonPropertyName]`). Don't reintroduce the
config binder. `nschema.json` keys are camelCase; `schema.dir` maps to `SchemaConfig.Directory` via `[JsonPropertyName]`.

## From config to a run

There is **no single superset config type**. Each command owns its own `*Configuration` model that implements
`IBindable` (`Configuration/Binding/IBindable` — one `void Bind(ParseResult)` method) and composes only the slices it
needs. The model **is** both the on-disk shape (STJ deserializes `nschema.json` into it) and the runtime shape: `Bind`
layers env + CLI on top. The dependency points one way (commands → factory), so adding or changing a command never
touches `ConfigurationFactory`.

Resolution is **vertically sliced into the command**: every command lives in its own `Commands/<Name>/` folder holding
the System.CommandLine `*Command` (option registration + handler), its `*Configuration` model, and its
`*ConfigurationValidator`. The handler's private `Resolve` calls `ConfigurationFactory.Load<TConfiguration>`, runs the
validator via `ValidateOrThrow`, and hands the validated model to the builder. A command's `Bind` is the composition
root: it calls each binding's `Bind` for its own scalar fields (e.g. `CommonOptions.Scope.Bind(result, s => Scope = s)`)
and delegates to each composed slice's `Bind` (`Provider.Bind(result)`, `State.Bind(result)`, …). The slices
(`SchemaConfig`, `ProviderConfig`, `StateConfig`, `ImportTargetConfig`) are the shared, reusable vocabulary — each is an
`IBindable` that owns binding its own nested sections (creating `provider.postgres`, `state.file`/`s3` on demand). A
command never sees a config field it has no use for.

Each command validator (**FluentValidation**) *composes the slice validators* (`SchemaConfigValidator`,
`ProviderConfigValidator`, `StateConfigValidator`, and their per-section leaf validators via `SetValidator`) and adds the
command's **presence** rules on top: `apply` requires a desired schema **and** a provider; `plan` requires a desired schema
**and** a current-schema source — a provider (live) **or** a state store (offline); `refresh` requires a provider **and** a
state store (it snapshots the **whole** live schema, so it takes no desired-schema or scope). Presence is expressed via each
config's `ConfiguredSectionCount` (the leaf validators reuse it for the "at most one section" rule). The projection runs
the validator through `ValidateOrThrow`, which throws a `ValidationException` with the joined `ErrorMessage`s — so an
invalid run fails before any builder call, and a future `validate` command can reuse the same per-command validators.

`CliApplicationBuilder` is a fluent wrapper around the core `NSchemaApplicationBuilder` that **trusts its inputs** — it no
longer validates. Each `Configure*` method takes exactly the slice it applies (`ConfigureDesiredSchema(SchemaConfig)`,
`ConfigureScope(string[]?)`, `ConfigurePolicies(DestructiveActionPolicy?)`, `ConfigureDatabaseProvider(ProviderConfig)`,
`ConfigureBackendState(StateConfig)`, `ConfigureConfirmation(bool)`); the command handler passes the corresponding field
off its validated model. There is no shared "configure everything" method, so adding a slice to one command does not affect
another. `ConfigureDatabaseProvider`/`ConfigureBackendState` dispatch on **which nested section is populated**
(`ProviderConfig.Postgres`, `StateConfig.File`/`S3`) rather than a discriminator enum, and bind through **property
patterns** (`{ Postgres: { ConnectionString: { } cs } }`) so there are no null-forgiving operators — the command validator
already guaranteed the shape. A zero-section provider/state is valid at the builder and means offline; it is the command
validators (not the builder) that reject it where a section is required.

The provider and state store are **defined in `nschema.json`**, not via CLI flags — they describe where the schema
lives, like a Terraform backend, so there are no `--provider`/`--state-*` options. The lone exception is the secret
connection string, which has an environment override: `NSCHEMA_POSTGRES_CONNECTION_STRING` is a self-identifying,
environment-only binding that fills `provider.postgres.connectionString` (via `ProviderConfig.EnsurePostgres()`, creating
the section if the file omitted it) — it names the Postgres provider on its own, just as `state.s3.*` names the S3 store,
so no discriminator flag is needed. Everything else about a section (e.g. `commandTimeout`, the chosen state store) is
file-only.

## Error handling and output

The CLI is the **single** presenter of errors. `Program.cs` disables System.CommandLine's default exception handler
(`EnableDefaultExceptionHandler = false`) and maps exceptions to exit codes (130 for cancellation, 1 otherwise). The core
is configured with `WithExceptionBehavior(ExceptionBehavior.Throw)` so it does **not** also print failures. Run output
(plan diffs, progress) flows through the core's `IMigrationReporter`; avoid direct `Console` writes except in `Program.cs`
(top-level errors) and the interactive prompt in `ConsoleMigrationConfirmation`.

## Options layout

Options are **colocated with the slice they configure** as `OptionBinding`s: `Configuration/Provider/ProviderOptions`,
`State/StateOptions`, `Schema/SchemaOptions`, `Import/ImportTargetOptions`, plus the cross-cutting
`Configuration/CommonOptions` (`Config`, `NoColor`, `Scope`, `Destructive`) and command-specific groups like
`Commands/Apply/ApplyOptions`. Each slice options class exposes `All` (an `IEnumerable<Option>` of its bindings'
`.Option`s); a `*Command.Create` registers a group with `command.Options.AddRange(ProviderOptions.All)` (the `AddRange`
extension lives in `Extensions/CommandExtensions`) and adds individual ones via `.Option` (e.g.
`CommonOptions.Scope.Option`). Env-var **names** stay centralized in `Configuration/EnvironmentVariables` as the
auditable surface; the bindings reference those constants.

## Desired-schema files

The files users point `--schema-dir` at are **`DatabaseSchema` documents** (YAML by default, or JSON). Column `type` is a
**compact string** (`bigint`, `text`, `varchar(255)`), not the `{ "kind": ... }` object form — that object form is the
state-snapshot serialization, which is a different thing. See `README.md` for a worked example.

## Test conventions

Tests use `// Arrange` / `// Act` / `// Assert` sections and a single member-level `_sut` field where the system under
test is an instance (static types use a small invocation helper instead). Mocks use NSubstitute, assertions use Shouldly.
Test parallelization is disabled assembly-wide because config resolution reads process-global environment variables; tests
that exercise env-var bindings (e.g. `OptionBindingTests`, `ConsoleFactoryTests`) snapshot and clear the variables they
touch in their constructor and `Dispose` to stay hermetic.
