# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`NSchema` is a thin command-line front-end (packaged as the `nschema` .NET global tool) over the **NSchema** core
framework — a declarative database schema migration engine ("Terraform for database schemas"). The CLI's job is to
resolve configuration, translate it into a core `NSchemaApplication`, and run one operation.

The core and providers (`NSchema`, `NSchema.Postgres`, `NSchema.Aws`) are consumed as **NuGet
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

A command's config is resolved in three layers, **lowest to highest precedence**: the project's **DSL configuration
blocks** (`NSCHEMA` / `PROVIDER` / `BACKEND`, declared in the `.sql` files), then **environment variables**, then
**command-line options**. `ConfigurationFactory.Load<T>(ParseResult)` drives it — it first honors **`--directory`** (the
recursive root option; it `SetCurrentDirectory`s so the project's `.sql` files and every relative path declared in them
resolve against the project dir, Terraform-`-chdir`-style — done here, the one chokepoint every command funnels through,
so it holds whether the CLI runs via `Program` or is invoked directly in a test). Then, **if `T` implements
`IDslConfigurable`**, it reads the project's config blocks (`DslProjectConfigReader` globs the `.sql` files, the core's
`DslConfigReader` captures the blocks, and `DslProjectConfigParser` maps them to a typed `DslProjectConfig`) and calls
`T.ApplyDsl(config)` to seed the model. Finally it calls `T.Bind(ParseResult)` to layer env + CLI over it. `T` is the
command's own `IBindable` config model (see below); the factory is generic and knows nothing about commands or slices.
There is **no `nschema.json` and no `--config`** — project configuration lives in the `.sql` files.

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
reading `--directory` and `ConsoleFactory`/`Program` reading `--no-color`.) Environment lookups are an allow-list: a binding
reads only the one variable it was given, named from the `EnvironmentVariables` constants — never raw strings.

Project configuration is **not** `Microsoft.Extensions.Configuration` and is **not** JSON. It is the DSL config blocks
the core captures (`DslConfigReader` → generic `ConfigBlock`s) and `DslProjectConfigParser` maps to typed slices —
strictly: unknown block types, labels, or attributes are errors, so typos surface rather than being silently ignored.
`init` scaffolds a `config.sql` with the starter `PROVIDER`/`BACKEND` blocks. Don't reintroduce a JSON config file or the
`Microsoft.Extensions.Configuration` binder.

## From config to a run

There is **no single superset config type**. Each command owns its own `*Configuration` model that implements
`IBindable` (`Configuration/Binding/IBindable` — one `void Bind(DslProjectConfig, ParseResult)` method) and composes only
the slices it needs. `ConfigurationFactory.Load<T>` reads the project's config blocks into a `DslProjectConfig`,
constructs `T`, and calls `T.Bind(project, cli)`; the binding layers the three sources — project config (lowest),
environment, then CLI (highest) — through `OptionBinding`s. The dependency points one way (commands → factory), so adding
or changing a command never touches `ConfigurationFactory`.

Resolution is **vertically sliced into the command**: every command lives in its own `Commands/<Name>/` folder holding
the System.CommandLine `*Command` (option registration + handler), its `*Configuration` model, its `*Options` (the
command's own option bindings), and its `*ConfigurationValidator`. The handler's private `Resolve` calls
`ConfigurationFactory.Load<TConfiguration>`, runs the validator via `ValidateOrThrow`, and hands the validated model to
the builder. A command's `Bind` is the composition root: it **composes the shared slices** (`Provider.Bind(project, cli)`,
`State.Bind(project, cli)`) and binds its **own** leaf flags through its command-local `*Options` straight onto the model
(e.g. `ApplyOptions.Scope.Bind(project, cli, s => Scope = s)`).

The shared slices `ProviderConfig` and `StateConfig` **bind themselves**: each implements `IBindable` and owns its
`OptionBinding`s as private statics co-located with the model, so a binding sourced purely from the project/env (the
Postgres connection string, the command timeout, the whole state store) is declared **once** rather than duplicated in
every command's `*Options`. This mirrors validation — a command's `Bind` recurses the slice tree exactly as its validator
composes the slice validators via `SetValidator`. A slice is `IBindable` only when its values come from that shared
project/env vocabulary; `ImportTargetConfig` stays **plain data** because its fields (`--output-file`, `--partition`, …)
are command-owned CLI flags that `ImportConfiguration` binds into it. Where a flat input must materialize a nested
section, the slice exposes a small accessor (`ProviderConfig.EnsurePostgres()`) its own `Bind` calls. A command never sees
a config field it has no use for.

Each command validator (**FluentValidation**) *composes the slice validators* (`ProviderConfigValidator`,
`StateConfigValidator`, and their per-section leaf validators via `SetValidator`) and adds the
command's **presence** rules on top: `apply` requires a provider; `plan` requires a current-schema source — a provider
(live) **or** a state store (offline); `refresh` requires a provider **and** a state store (it snapshots the **whole**
live schema, so it takes no scope). The **desired schema is not a config concern** — it is always the `*.sql` files under
the project directory, so no command validates its presence (the builder guards the zero-files case instead; see below).
Presence is expressed via each config's `ConfiguredSectionCount` (the leaf validators reuse it for the "at most one section" rule). The projection runs
the validator through `ValidateOrThrow`, which throws a `ValidationException` with the joined `ErrorMessage`s — so an
invalid run fails before any builder call, and a future `validate` command can reuse the same per-command validators.

`CliApplicationBuilder` is a fluent wrapper around the core `NSchemaApplicationBuilder` that **trusts its inputs** — it no
longer validates config. Each `Configure*` method takes exactly the slice it applies (`ConfigurePolicies(DestructiveActionPolicy?)`,
`ConfigureDatabaseProvider(ProviderConfig)`, `ConfigureBackendState(StateConfig)`, `ConfigureConfirmation(bool)`); the
command handler passes the corresponding field off its validated model. The lone exception is `ConfigureDesiredSchema()`
— it takes no slice (the desired schema is always the recursive `*.sql` glob of the working directory) and throws if
**no** schema files are found, since an empty desired schema would otherwise read as "drop everything". There is no
shared "configure everything" method, so adding a slice to one command does not affect
another. `ConfigureDatabaseProvider`/`ConfigureBackendState` dispatch on **which nested section is populated**
(`ProviderConfig.Postgres`, `StateConfig.File`/`S3`) rather than a discriminator enum, and bind through **property
patterns** (`{ Postgres: { ConnectionString: { } cs } }`) so there are no null-forgiving operators — the command validator
already guaranteed the shape. A zero-section provider/state is valid at the builder and means offline; it is the command
validators (not the builder) that reject it where a section is required.

The provider and state store are **declared in `PROVIDER` / `BACKEND` config blocks** in the `.sql` files, not via CLI
flags — they describe where the schema lives, like a Terraform backend, so there are no `--provider`/`--state-*` options.
The connection string also has an environment override: `NSCHEMA_POSTGRES_CONNECTION_STRING` is a self-identifying,
environment-only binding that fills the Postgres connection string (via `ProviderConfig.EnsurePostgres()`, creating the
section if the block omitted it) and **overrides** a `connection_string` set in the block — it names the Postgres provider
on its own, just as `BACKEND s3` names the S3 store, so no discriminator flag is needed. Everything else about a section
(e.g. `command_timeout`, the chosen state store) comes from the config block. A `connection_string` **is** allowed in a
`PROVIDER` block (for local/single-file setups); the env var is preferred for real secrets.

**Credentials may be supplied separately from the connection string.** `NSCHEMA_POSTGRES_USERNAME` /
`NSCHEMA_POSTGRES_PASSWORD` (also expressible as `username` / `password` block attributes) are discrete overrides that
layer onto the base connection string — for secret stores (e.g. AWS Secrets Manager) that inject credentials out of band
while the block carries only the non-secret host/port/database. This works because the core hands
`ConfigureDatabaseProvider` an Npgsql `NpgsqlConnectionStringBuilder`, whose typed properties (`Username`, `Password`,
`CommandTimeout`, …) each override the corresponding key parsed from `ConnectionString`. **Order matters:** the builder
sets `.ConnectionString` *first*, then the individual properties — assigning `.ConnectionString` re-parses the whole
string, so it must precede the discrete overrides or it would clobber them. This is the convention for any new
provider's settings: **secrets → env-overridable named bindings; structural config → the DDL block** (both expressible
either way, but steer secrets to env), and **base config set first, discrete overrides layered after**. A connection
parameter earns a separate env override only when a real environment splits it out — don't add `host`/`port`/etc.
speculatively, since each env var is permanent API surface. Adding a *new* provider is a vertical slice touching its
config model, `ProviderConfig.Bind`, `DdlProjectConfigReader.ParseProvider`, the leaf validator,
`ConfigureDatabaseProvider`, and the `EnvironmentVariables` constants — deliberately, the same slice-over-abstraction
trade made elsewhere; resist extracting a shared provider abstraction until a second provider makes the real seam
concrete.

## Error handling and output

The CLI is the **single** presenter of errors. `Program.cs` disables System.CommandLine's default exception handler
(`EnableDefaultExceptionHandler = false`) and maps exceptions to exit codes (130 for cancellation, 1 otherwise). The core
is configured with `WithExceptionBehavior(ExceptionBehavior.Throw)` so it does **not** also print failures. Run output
(plan diffs, progress) flows through the core's `IMigrationReporter`; avoid direct `Console` writes except in `Program.cs`
(top-level errors) and the interactive prompt in `ConsoleMigrationConfirmation`.

## Options layout

Options split by **source**, not just by command. A command's own CLI **flags** — `--scope`, `--auto-approve`,
`--destructive-actions`, etc. — are owned by its `Commands/<Name>/<Name>Options` class, one `OptionBinding` each, with the
description tailored to that command. Duplication of a flag across commands (both `apply` and `plan` declare their own
`--scope`) is the deliberate cost of per-command contextual help. Bindings whose value comes purely from the project
config / environment and carry **no** CLI option or description — the Postgres connection string and command timeout, the
state store — are **not** duplicated per command: they live once on the shared slice that owns them (`ProviderConfig`,
`StateConfig`) as private statics, and the slice binds itself (see above). The rule: co-locate the binding on the slice
when there is no per-command contextual help to lose; keep it per-command when there is. `Configuration/CommonOptions`
holds only the harness-level flags that are **not** bound to any config slice: `Directory` and `NoColor`, both read
recursively at the root. Each `*Options` exposes `All` (an `IEnumerable<Option>` of its **CLI** bindings — a slice's
self-bound project/env bindings register no CLI option at all); `*Command.Create` registers it with
`command.Options.AddRange(<Name>Options.All)` (the `AddRange` extension lives in `Extensions/CommandExtensions`), while
the root command adds `CommonOptions.NoColor.Option` and `CommonOptions.Directory.Option` recursively. Env-var **names**
stay centralized in `Configuration/EnvironmentVariables` as the auditable surface; the bindings reference those constants.

## Desired-schema files

The desired schema is every `*.sql` file found recursively under the project directory (the `--directory` root),
written in **NSchema DDL** — the core's canonical, SQL-flavoured `DatabaseSchema` serialization (format key `"sql"`,
registered by the core; the CLI no longer offers YAML or JSON). Column types are canonical compact strings (`bigint`,
`text`, `varchar(255)`). There is no format, directory, or glob to configure. See `README.md` for a worked example.

**Deployment scripts** are raw SQL files distinguished by suffix: `*.pre.sql` runs before the migration, `*.post.sql`
after (both in filename order, registered via the core's `AddScriptFromFile`/`ScriptType`). They are the imperative
escape hatch (extensions, backfills, grants NSchema doesn't model) and are deliberately **excluded** from the desired
schema by `ConfigureDesiredSchema` (so the DSL parser never sees them). `ConfigureScripts` registers them, and is
called only by the deployment commands — `apply` and forward `plan` — not by `validate`, `destroy`, or `plan --destroy`.
They run on every apply, so they must be idempotent.

## Test conventions

Tests use `// Arrange` / `// Act` / `// Assert` sections and a single member-level `_sut` field where the system under
test is an instance (static types use a small invocation helper instead). Mocks use NSubstitute, assertions use Shouldly.
Test parallelization is disabled assembly-wide (`[assembly: CollectionBehavior(DisableTestParallelization = true)]` in
`AssemblyInfo.cs`) because config resolution reads and mutates process-global state — environment variables, and the
current working directory via `--directory`. Tests that touch that state restore it: env-var tests (e.g.
`OptionBindingTests`, `ConsoleFactoryTests`) snapshot and clear the variables they use in their constructor and `Dispose`,
and the cwd-changing tests (`ConfigurationFactoryTests`, `MigrationRoundTripTests`) save and restore the working directory.
