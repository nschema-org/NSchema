# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`NSchema` is a thin command-line front-end (packaged as the `nschema` .NET global tool) over the **NSchema** core
framework — a declarative database schema migration engine ("Terraform for database schemas"). The CLI's job is to
resolve configuration, translate it into a core `NSchemaApplication`, and run one operation.

The CLI consumes **`NSchema.Core` as a pinned NuGet package** (versions in `Directory.Packages.props`); its source lives
in the sibling repo `../NSchema.Core`. As of **v4 it no longer references the providers or backends** — those are
**separate NuGet packages loaded at runtime as plugins** (see *Provider & backend plugins* below), so a database engine
ships and versions independently of the CLI. Changing core behavior still requires publishing a new core package and
bumping the pinned version here.

## Where an operation lives: Core vs CLI

A command's *logic* lives in `NSchema.Core` (invoked via a public `NSchemaApplication.X(...)` method that resolves an
`IXOperation` from DI) or in this CLI repo (a self-contained `*Command`). The dividing axis is **orchestration
complexity, not whether it touches live infrastructure** — taking a lock touches infra but is a one-call primitive,
while `validate` touches no infra yet orchestrates parse+diff. Applied **in order**:

1. **Does it orchestrate a reusable multi-step sequence** — composing the provider, planner, and/or state store into a
   pipeline whose result any front-end (GUI, CI library) must reproduce identically? → **Core operation.** *(apply,
   plan, plan --destroy, drift, refresh, destroy, import, doctor; and validate, which parses+diffs the
   `DatabaseSchema` despite touching no infrastructure.)*
2. **Else, is it a thin pass-through to an existing Core primitive** — a single public interface call (e.g.
   `app.Locks`'s `Acquire`/`Peek`/`Release`, `app.CurrentSchema.GetSchema`, `app.PlanFile.Read`) plus presentation
   — **or local developer plumbing** (filesystem scaffolding, source-text formatting, shell integration,
   config/IO/rendering, **plugin resolution & cache management**)? → **CLI command.** *(lock status / acquire / release —
   thin over the public `IStateLockCoordinator` (`app.Locks`); show — thin over `app.CurrentSchema`/`app.PlanFile`;
   state pull / push and script list / taint / untaint — read → mutate → write loops over the public
   `ISchemaStateManager` (`app.State`), with untaint taking the declaration's body hash from `app.DesiredSchema`;
   plugin list / show / cache list / remove / clear — thin over the local plugin cache (`PluginCache`) and
   project config; init, fmt, completion.)*

The reusable behaviour for these commands lives in Core (the contracts and their implementations); the CLI command is
just a caller, so there's no Core operation to wrap it. Exposing a primitive publicly for the CLI to consume is a
deliberate API decision (e.g. `show` re-publicized `ICurrentSchemaProvider`/`IPlanFileWriter`/`PlanFileEnvelope`) — weigh
it against API-surface stability, not just the boundary rule.

**Presentation lives in the CLI**, split by *what* is being written (`Services/Reporting/`), with a Spectre face and a
`--json` face for each:

- **`IConsoleMessenger`** — line-level narration (status `Report`, `Announce`/`Success`/`Warn`/`Detail`,
  `ReportDiagnostics`, the lock/plugin query renderers). App-free: built straight from the `ParseResult` by
  `ReporterFactory`, so it works before/without an application (top-level error handling in `Program.cs`, the `plugin`
  commands). With an app it's reached as **`app.Messenger`**.
- **`IConsolePresenter`** — an operation's structured output (`ReportDiff`/`ReportSchema`/`ReportSqlPlan`/`ReportPlan`,
  plus `ReportSavedPlan` for `plan show`). It owns the core renderers directly — `DiffRenderer`/`SchemaRenderer`/
  `SqlPlanRenderer` via their `.Default` singletons, stateless utilities rather than DI services — and wraps their
  plain-text output in Spectre markup. Reached as **`app.Presenter`**.
- The messenger and presenter are **stateless console utilities the CLI owns directly, not container services**:
  `ReporterFactory` builds the right (Spectre or `--json`) pair, and they hang off the **`CliApplication`** handle
  (`app.Messenger`/`app.Presenter`) next to the engine members it forwards (`app.Operations`/`app.Locks`/
  `app.CurrentSchema`/`app.PlanFile`). `CliApplication` is what `CliApplicationBuilder.Build()` returns — the built core
  `NSchemaApplication` paired with the console surfaces, so a command reaches engine and console through one handle.
- **Core-operation progress** (the live narration a long run emits) flows through the core's `IProgress<OperationProgress>`,
  implemented CLI-side by `Services/Reporting/ConsoleProgress` (wrapping the messenger) and registered via the builder's
  `UseProgressReporter(new ConsoleProgress(messenger))`.

The `--json` shape splits on the **nature of the command**, not the method. A *progressive operation* (`apply`, `plan`,
`destroy`, `drift`) emits an NDJSON stream of `{"type":…}` events on stdout — so `ReportDiff`/`ReportPlan`/`ReportSqlPlan`
each carry a discriminator. A *query* (`db show`, `state show`, `plan show`, `lock status`, `script list`, `plugin …`) is one request
for one answer, so it emits a **single bare object** on stdout (no `type` envelope): `ReportSchema` writes the schema
directly, and `plan show` uses `ReportSavedPlan` to fold its diff + scripts + SQL into one `{diff, scripts, sql}` object
rather than three lines. Either way, line-level narration (`Announce`/etc.) goes to **stderr** as the gated `{"type":"log"}`
stream — so `cmd --json | jq` only ever sees the result, never the narration.

## Commands

```sh
dotnet build NSchema.slnx
dotnet test  NSchema.slnx                                   # all tests
dotnet test  NSchema.slnx --filter "FullyQualifiedName~OptionBindingTests"   # one class
dotnet test  NSchema.slnx --filter "FullyQualifiedName~RootCommandTests.HasTheNschemaCommandName"  # one test
```

- **`dotnet test` no longer needs Docker** — the provider round-trip suites moved to the provider repos in v4. The one
  integration test, `PluginLoaderTests`, restores a real plugin via `dotnet publish`, so it needs the **.NET SDK and
  network access** (it reaches nuget.org); everything else is a pure unit test.
- `TreatWarningsAsErrors` and `GenerateDocumentationFile` are on — builds fail on warnings, and the build packs the tool
  (`GeneratePackageOnBuild`). Target framework is `net10.0`.

## Configuration resolution (the heart of the CLI)

Project configuration lives in the project's `.sql` files as **`PROVIDER` / `BACKEND` config blocks** (config-in-SQL —
there is no `nschema.json`, no `--config`, and as of v4 **no `NSCHEMA` block**). `ConfigurationFactory.Load<T>(ParseResult)`
drives resolution. It first honors **`--directory`** (the recursive root option; it `SetCurrentDirectory`s so the
project's `.sql` files and the relative paths in them resolve against the project dir, Terraform-`-chdir`-style — the one
chokepoint every command funnels through, so it holds whether the CLI runs via `Program` or is invoked directly in a
test). It then reads the config blocks via `DdlProjectConfigReader` (globs the `.sql` files; the core captures generic
`ConfigBlock`s; the reader maps `PROVIDER` → a `PluginReference` and `BACKEND` → a `StateConfig`), producing a typed
`DdlProjectConfig`. Finally it constructs `T` and calls `T.Bind(project, cli)`.

Two kinds of config are resolved differently:

- **Where the schema lives** (`PROVIDER` / `BACKEND`) is **project-only** — `T.Bind` copies it straight off the
  `DdlProjectConfig` (`Provider = project.Provider; State = project.State;`), with no CLI-level env/CLI override. A
  `PROVIDER` block names a plugin by label, **pins its package `version`** (optionally `source`), and carries the
  provider's own settings (connection string, etc.); those settings are read by **the plugin**, not the CLI (the plugin
  also owns its own `NSCHEMA_<PROVIDER>_*` env vars). A `PROVIDER` block is **mandatory** to use a provider — the
  connection-string env var no longer self-identifies one.
- **Command leaf flags** (`--scope`, `--destructive-actions`, `--auto-approve`, …) are resolved per-flag through
  `Configuration/Binding/OptionBinding<T>`, which layers **environment variable < CLI option** (CLI wins; env via the
  `EnvironmentVariables` allow-list). As of v4 `OptionBinding` has **no project-config layer** — the only setting that
  used it, `destructive_action`, moved fully to the flag / env var.

`OptionBinding<T>` owns a single binding: an optional System.CommandLine `Option<T>`, an optional env var, and a parser.
Built fluently (`OptionBinding.Create<T>().FromOption("--x").FromEnvironmentVariable(EnvVar).WithDescription(...)`);
`.AllowMultipleArguments()`/`.Recursive()` configure the lazily-built, cached `.Option`. A binding with only
`.FromEnvironmentVariable` is environment-only (`.Option` throws). `Bind(cli, apply)` calls `apply` only when env or CLI
supplies a value; `TryGetValue`/`GetValueOrDefault(cli, …)` expose the same resolution for `--directory`/`--no-color`.
Env parsing is automatic (enums case-insensitively, strings by identity; pass a parser for other types).

## From config to a run

There is **no single superset config type**. Each command owns its own `*Configuration` model (`Commands/<Name>/`)
implementing `IBindable` (`void Bind(DdlProjectConfig, ParseResult)`) and composing only what it needs. A command's
`Bind` assigns the project slices directly and binds its **own** leaf flags through its command-local `*Options`
(`Provider = project.Provider; State = project.State; ApplyOptions.Scope.Bind(cli, s => Scope = s);`). The handler's
`Resolve` calls `ConfigurationFactory.Load<TConfiguration>`, runs the FluentValidation `*ConfigurationValidator` via
`ValidateOrThrow`, and hands the validated model to `CliApplicationBuilder`.

The provider/backend slices are now **plain data**, not `IBindable`:

- **Provider** is a `PluginReference?` (`Configuration/Plugins/PluginReference`) — the resolved package id, pinned
  `version`, label, and the block's remaining attributes (with `version`/`source` stripped, so the plugin never sees
  them). `null` means offline. `PluginReference.FromBlock` resolves it: a built-in label maps to its first-party package
  (`postgres → NSchema.Postgres`, …; the maps live on `PluginReference.ForProvider`/`ForBackend`), a `source` attribute
  overrides it with any package, and a `version` is required. There is **no `ProviderConfig` slice**.
- **State** is a `StateConfig?` — a small union of `FileStateConfig? File` (the built-in local-file store) **xor**
  `PluginReference? Plugin` (every other backend, e.g. `s3 → NSchema.Aws`). `null` means online-only. `file` is the lone
  built-in: no plugin, no `version`.

Presence is just a null check (`Provider is not null`, `State is not null`) — there is no `ConfiguredSectionCount` /
`IsConfigured`. Each command validator adds its **presence** rules on top: `apply` requires a provider
(`RuleFor(x => x.Provider).NotNull()`); `plan` requires a current-schema source — a provider (live) **or** a state store
(offline); `refresh`/`drift` require both. The desired schema is **not** a config concern (always the recursive `*.sql`
glob), so no command validates its presence — the builder guards the zero-files case (an empty desired schema would read
as "drop everything").

`CliApplicationBuilder` wraps the core `NSchemaApplicationBuilder` and **trusts its validated inputs**.
`ConfigureDatabaseProvider(PluginReference?)` / `ConfigureBackendState(StateConfig?)` resolve the plugin and apply it;
`ConfigurePolicies(DestructiveActionPolicy?)` / `ConfigureConfirmation(bool)` take the command's flag-resolved values.
The file backend applies directly via the core's `UseFileStateStore`; a `null` provider/state is valid and means offline.

### Provider & backend plugins

A provider/backend is a NuGet package implementing a contract from `NSchema.Core`: **`INSchemaProviderPlugin`**
(introspection + SQL generation) or **`INSchemaBackendPlugin`** (state store), both exposing a `Label`, a
`GetScaffoldTemplate(ScaffoldContext)`, and a `Configure(NSchemaApplicationBuilder, ConfigBlock)` returning a
`PluginConfigureResult` (success, or aggregated errors — it does **not** throw, so `doctor` can report a misconfigured
plugin). `Configuration/Plugins/PluginLoader` turns a `PluginReference` into live instances: it synthesizes an
`EnableDynamicLoading` project, shells `dotnet publish` to materialise the pinned package's dependency closure into a
per-version cache under `~/.nschema/plugins` (whose on-disk layout is owned by `Configuration/Plugins/PluginCache` — the
loader delegates all path math to it), and loads it into an isolated `AssemblyLoadContext` that **defers
`NSchema.Core` + the framework assemblies to the host** (so contract types unify across the boundary) while isolating the
plugin's own deps (Npgsql, the AWS SDK, …). The host rejects a plugin whose referenced `NSchema.Core` **major** differs.
`CliApplicationBuilder.ResolvePlugin` matches the discovered plugin by label and calls `Configure`; a failed result
becomes a thrown error (the CLI is the single error presenter). The provider's config vocabulary (block attributes + its
own env vars) and validation live **in the plugin**, not the CLI.

**Adding a provider/backend is a new package**, not a CLI change: implement the contract, and either name it via the
block's `source` attribute or (for a first-party engine) add it to the built-in label→package map on `PluginReference`.

The **`plugin` command group** (`Commands/Plugin/`) is the management surface over this, all thin CLI per the boundary
rule (config + cache inspection, no Core op): `plugin list` / `plugin show <label>` cross-reference the project's pinned
plugins (via `PluginInventory.ForProject`) against the cache; `plugin cache list` / `remove <package> [version]` / `clear`
operate on the profile-level `PluginCache` directly. The cache is **shared across projects**, so there is no per-project
prune — only `cache remove` (targeted) and `cache clear` (wholesale). `init` is the restore counterpart.

## Error handling and output

The CLI is the **single** presenter of errors. `Program.cs` disables System.CommandLine's default exception handler
(`EnableDefaultExceptionHandler = false`) and maps the genuinely-exceptional escapes to exit codes (130 for cancellation,
1 otherwise). Core operations return `Result`s — an *expected* failure (contention, a policy violation, a bad config)
comes back as failure diagnostics the command renders, not a thrown exception and not a core-side print. Structured
run output (the diff, schema, SQL plan) is rendered by the CLI's `app.Presenter`, and live progress flows through the
core's `IProgress<OperationProgress>` (the CLI's `ConsoleProgress`); avoid direct `Console` writes except in `Program.cs`
(top-level errors) and the interactive prompt in `ConsoleConfirmationPrompt`.

## Options layout

Options split by **source**, not just by command. A command's own CLI **flags** — `--scope`, `--auto-approve`,
`--destructive-actions`, etc. — are owned by its `Commands/<Name>/<Name>Options` class, one `OptionBinding` each, with the
description tailored to that command. Duplication of a flag across commands (both `apply` and `plan` declare their own
`--scope`) is the deliberate cost of per-command contextual help. (Provider/backend settings are **not** CLI bindings at
all — they live in the `PROVIDER`/`BACKEND` blocks and are read by the plugin; the CLI keeps only command flags plus the
harness-level options.) `Configuration/CommonOptions` holds the harness-level flags not bound to any command:
`Directory` and `NoColor`, read recursively at the root. Each `*Options` exposes `All` (its CLI bindings);
`*Command.Create` registers it with `command.Options.AddRange(<Name>Options.All)` (the `AddRange` extension lives in
`Extensions/CommandExtensions`), while the root command adds `CommonOptions.NoColor.Option` and
`CommonOptions.Directory.Option` recursively. Env-var **names** stay centralized in `Configuration/EnvironmentVariables`
as the auditable surface; the bindings reference those constants. (Provider-specific env vars — `NSCHEMA_<PROVIDER>_*` —
are owned by the plugins, not listed here.)

## Desired-schema files

The desired schema is every `*.sql` file found recursively under the project directory (the `--directory` root),
written in **NSchema DDL** — the core's canonical, SQL-flavoured `DatabaseSchema` serialization (format key `"sql"`,
registered by the core; the CLI no longer offers YAML or JSON). Column types are canonical compact strings (`bigint`,
`text`, `varchar(255)`). There is no format, directory, or glob to configure. See `README.md` for a worked example.

**Scripts** are declared **inline** in the DDL with the unified `SCRIPT '<name>' RUN [ALWAYS | ONCE] ON <event>
[(run_outside_transaction = true)] AS $$…$$;` statement (Core 4.4+). The event is a deployment bookend
(`PRE DEPLOYMENT` / `POST DEPLOYMENT`) or a structural change (`ADD COLUMN` / `ALTER COLUMN TYPE` / `ADD CONSTRAINT`
with a `schema.table.member` path — the data-migration form, spliced into the plan only when the matching change is
planned). `RUN ONCE` scripts are recorded in the state backend on a successful apply and skipped by later plans (skip
= `run-once` Info diagnostic; a changed body warns and stays skipped). Script names are unique project-wide. The
pre-4.4 spellings (`PRE|POST DEPLOYMENT 'name' AS $$…$$;`, `MIGRATION ['name'] FOR <trigger> <path> AS $$…$$;`) still
parse but surface `deprecations` warnings — removal in 5.0. The CLI does **almost nothing special** with any of this:
scripts ride the same `*.sql` glob into the core's parser, the core plans/executes/records them (the run-once manifest
travels on `SqlPlan` inside the plan result and plan file, so apply needs no extra wiring), and the CLI's only additions
are presentation — the pre/post plan sections annotate run-once scripts (`(run once)`; `runCondition` in `--json`), and
the `run-once`/`deprecations` diagnostics render through the standard diagnostics table.

The **`script` command group** (`Commands/Script/`) manages the recorded ledger, all thin CLI over `app.State`
(`ISchemaStateManager`): `script list` renders the recorded executions (a query — single bare array in `--json`),
`script taint <name>` removes an execution (read → `RemoveScript` → write), and `script untaint <name>` records a
run-once script as executed without running it — the name + body `Hash` come from the script's declaration, read
through `app.DesiredSchema` (the expanded desired project), so no provider or plan is involved; untaint on an
already-recorded script deliberately errors with "taint first, then untaint" rather than silently overwriting the
recorded hash. `script hash [name]` computes the same declaration hashes for hand-editing pulled state (bare hash on
stdout with a name, table/array without) — the shared name-matching lives in `RunOnceDeclarations`, used by both
hash and untaint. `state pull` / `state push` move the raw payload through `ReadRaw`/`WriteRaw` — pull suppresses
narration when writing to stdout so redirection stays byte-clean; push validates and writes verbatim. Push, taint,
and untaint take the state lock (each has `--no-lock`); pull and list are reads and do not.

## Test conventions

Tests use `// Arrange` / `// Act` / `// Assert` sections and a single member-level `_sut` field where the system under
test is an instance (static types use a small invocation helper instead). Mocks use NSubstitute, assertions use Shouldly.
Test parallelization is disabled assembly-wide (`[assembly: CollectionBehavior(DisableTestParallelization = true)]` in
`AssemblyInfo.cs`) because config resolution reads and mutates process-global state — environment variables, and the
current working directory via `--directory`. Tests that touch that state restore it: env-var tests (e.g.
`OptionBindingTests`, `ConsoleFactoryTests`) snapshot and clear the variables they use in their constructor and `Dispose`,
and the cwd-changing tests (`ConfigurationFactoryTests`) save and restore the working directory.
