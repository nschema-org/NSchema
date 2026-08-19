# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`NSchema` is a thin command-line front-end (packaged as the `nschema` .NET global tool) over the **NSchema** core
framework — a declarative database schema migration engine ("Terraform for database schemas"). The CLI's job is to
resolve configuration, translate it into a core `NSchemaApplication`, and run one operation.

The CLI consumes **`NSchema.Core` as a project reference** — the core's source lives in this repo under
`src/NSchema.Core` (its own `CLAUDE.md` is there), and a release tags once and publishes the `nschema` tool and the
`NSchema.Core` package at the same version. As of **v4 it no longer references the providers or backends** — those are
**separate NuGet packages loaded at runtime as plugins** (see *Provider & backend plugins* below), so a database engine
ships and versions independently of the CLI.

## Where an operation lives: Core vs CLI

A command's *logic* lives in `NSchema.Core` (invoked via a public `NSchemaApplication.X(...)` method that resolves an
`IXOperation` from DI) or in this CLI repo (a self-contained `*Command`). The dividing axis is **orchestration
complexity, not whether it touches live infrastructure** — taking a lock touches infra but is a one-call primitive,
while `validate` touches no infra yet orchestrates parse+diff. Applied **in order**:

1. **Does it orchestrate a reusable multi-step sequence** — composing the provider, planner, and/or state store into a
   pipeline whose result any front-end (GUI, CI library) must reproduce identically? → **Core operation.** *(apply,
   plan, plan --destroy, drift, refresh, destroy, import, doctor; and validate, which parses the project and runs its
   policies despite touching no infrastructure.)*
2. **Else, is it a thin pass-through to an existing Core primitive** — a single public interface call (e.g.
   `app.Locks`'s `Acquire`/`Peek`/`Release`, `app.Database.GetSchema`, `app.PlanFile.Read`) plus presentation
   — **or local developer plumbing** (filesystem scaffolding, source-text formatting, shell integration,
   config/IO/rendering, **plugin resolution & cache management**)? → **CLI command.** *(lock status / acquire / release —
   thin over the public `IStateLockManager` (`app.Locks`); show — thin over `app.Database`/`app.PlanFile`;
   state pull / push and script list / taint / untaint — read → mutate → write loops over the public
   `IDatabaseStateManager` (`app.State`), with untaint taking the declaration's body hash from `app.ProjectDefinition`;
   plugin list / show / cache list / remove / clear — thin over the local plugin cache (`PluginCache`) and
   project config; init, format, completion.)*

The reusable behaviour for these commands lives in Core (the contracts and their implementations); the CLI command is
just a caller, so there's no Core operation to wrap it. Exposing a primitive publicly for the CLI to consume is a
deliberate API decision (e.g. `show` re-publicized `IDatabaseProvider`/`IPlanFileManager`/`PlanFileEnvelope`) — weigh
it against API-surface stability, not just the boundary rule.

**Presentation lives in the CLI** as **one seam**: `IConsoleReporter` (`Services/Reporting/`), with one implementation
per output format — `SpectreConsoleReporter` (text), `JsonConsoleReporter` (`--json`), `MarkdownConsoleReporter`
(`--format markdown`). There is no messenger/presenter split; the earlier one sorted by "app-free vs app-bound", which
was never a real constraint (no face needs the application), so members drifted across it.

Every output belongs to exactly one of **three classes**, and the class — never the call site — decides how verbosity
applies. **A command never checks `Verbosity`**; it calls the reporter and the face decides.

1. **Narration** — the kind-classified line methods (`Report`, `Announce`/`Success`/`Warn`/`Detail`,
   `ReportEnvironment`). Presence is gated by the `Verbosity` predicate over `MessageKind`; quiet shows only
   `Success`/`Warning`. Every narration passes through one chokepoint per face, so the rule is total — there is no
   "show this one anyway" escape, and a message that should survive quiet earns it by being the right `MessageKind`.
2. **Artifacts** — the `Report*` methods (plan, diff, schema, state, script ledger, script hashes, lock info, plugin
   reports, diagnostics). **Never suppressed**; verbosity selects *rendering depth*. The text faces render a one-line
   summary under `--quiet` (`Plan: 2 to add, 1 to change, 0 to destroy (5 statements).`); the **JSON face ignores
   verbosity entirely** — its artifacts are the machine contract and are always complete. Diagnostics summarize by
   dropping the Info rows. Single-object artifacts (lock info, plugin detail) have one face: their full rendering
   already is the summary.
3. **Interaction** — `Confirm(ConfirmationRequest)`. Never suppressed while it actually prompts: whatever the
   verbosity, an operator typing "yes" sees what they are approving. Pre-approved (`--auto-approve`) it degenerates to
   narration and gates with it, so `--quiet --auto-approve` is silent. The command supplies facts
   (`ConfirmationRequest`: summary, question, skip flag, `Destructive`); the **face** owns the wording and styling, so
   commands never pre-bake markup. The JSON/Markdown faces prompt on **stderr** and, with no terminal, report the
   summary as a log event rather than raw text — stdout stays a byte-clean result stream either way.

`ReportException` is never gated at all.

The reporter is a **stateless console utility the CLI owns directly, not a container service**: `ReporterFactory` is
the single construction point (`CreateReporter(ParseResult)` / `CreateReporter(OutputFormat, Verbosity)`), used by the
builder, `CommandRunner`'s preamble, `Program.cs`'s top-level error handling, and the odd app-free command alike. It
hangs off the **`CliApplication`** handle as **`app.Reporter`**, next to the engine members it forwards
(`app.Operations`/`app.Locks`/`app.Database`/`app.PlanFile`). `CliApplication` is what `CliApplicationBuilder.Build()`
returns — the built core `NSchemaApplication` paired with the console, so a command reaches engine and console through
one handle. **Core-operation progress** flows through the core's `IProgress<OperationProgress>`, implemented CLI-side
by `Services/Reporting/ConsoleProgress` (wrapping the reporter) and registered via `UseProgressReporter`.

Wording shared between faces and tenses lives in small statics beside them — `PlanNarrative` (prospective counts, the
quiet summary, *and* the retrospective `Describe` recap, so a plan is counted one way everywhere), `DatabaseNarrative`,
`ScriptLedger`, `SqlPlanNarrative`.

The `--json` shape follows **what is being reported**, not which command asked. Every artifact writes a
**single bare object** (or array) on one line of stdout, keyed for the artifact: `ReportDatabase` writes the schema
directly, `ReportDiff` the diff, `ReportPlan` `{diff, adopted, sql}`, `ReportState` `{database, managed, scripts}`,
and `ReportScripts` a bare array — so `cmd --json | jq` reads a result without unwrapping a discriminator. NDJSON
still frames a run that reports more than once (each report is its own complete line). Line-level narration
(`Announce`/etc.) goes to **stderr** as the gated `{"type":"log"}` stream, where the discriminator does the work —
so `cmd --json | jq` only ever sees the result, never the narration.

## Commands

```sh
dotnet build NSchema.slnx
dotnet test  NSchema.slnx                                   # all tests
dotnet test  NSchema.slnx --filter "FullyQualifiedName~OptionBindingTests"   # one class
dotnet test  NSchema.slnx --filter "FullyQualifiedName~RootCommandTests.HasTheNschemaCommandName"  # one test
```

- The CLI test project needs no Docker — the provider round-trip suites moved to the provider repos in v4. Its one
  integration test, `PluginLoaderTests`, restores a real plugin via `dotnet publish`, so it needs the **.NET SDK and
  network access** (it reaches nuget.org). `NSchema.Core.Tests` runs Testcontainers-based integration tests, so the
  full-solution `dotnet test` needs **Docker**.
- `TreatWarningsAsErrors` and `GenerateDocumentationFile` are on — builds fail on warnings, and the build packs the tool
  (`GeneratePackageOnBuild`). Target framework is `net10.0`.

## Configuration resolution (the heart of the CLI)

Project configuration lives in the project files — as **`DATABASE` / `STATE` statements**.
`ConfigurationFactory.Load<T>(ParseResult)` drives resolution. It first honors **`--directory`** (the recursive root
option; it `SetCurrentDirectory`s so the project's files and the relative paths in them resolve against the
project dir, Terraform-`-chdir`-style — the one chokepoint every command funnels through, so it holds whether the CLI
runs via `Program` or is invoked directly in a test). It then reads the configuration statements via
`ProjectConfigReader` (globs the config files, layering an environment's overlay over the base; the core parses them
into a config definition; the reader maps `DATABASE` → a `PluginReference` and `STATE` → a `StateConfig`), producing a
typed `ProjectConfig`. Finally it constructs `T` and calls `T.Bind(project, cli)`.

Two kinds of config are resolved differently:

- **Where the schema lives** (`DATABASE` / `STATE`) is **project-only** — `T.Bind` copies it straight off the
  `ProjectConfig` (`Provider = project.Provider; State = project.State;`), with no CLI-level env/CLI override. A
  `DATABASE` statement names a plugin by label and carries the provider's own settings (connection string, etc.); a
  matching `PLUGIN <label> ( source = '…', version = '…' );` declaration pins the package (its `version`, and optional
  `source`). Those settings are read by **the plugin**, not the CLI (the plugin also owns its own
  `NSCHEMA_<PROVIDER>_*` env vars). A `DATABASE` statement is **mandatory** to use a provider — the connection-string
  env var no longer self-identifies one.
- **Command leaf flags** (`--scope`, `--destructive-actions`, `--auto-approve`, …) are resolved per-flag through
  `Configuration/Binding/OptionBinding<T>`, which layers **environment variable < CLI option** (CLI wins; env via the
  `EnvironmentVariables` allow-list). As of v4 `OptionBinding` has **no project-config layer** — the only setting that
  used it, `destructive_action`, moved fully to the flag / env var.
- **Diagnostic severities** are read from the project's **`.editorconfig`** by `Configuration/EditorConfigReader` —
  `nschema_diagnostic.<code>.severity` and `nschema_diagnostic_source.<source>.severity`, taking Roslyn's severity
  words, because the file will also carry NSQL formatting rules and is what an editor/LSP already reads. It resolves
  the chain **per schema file** (the only way the globs mean what they say) and then **requires the results to agree**:
  enforcement is applied to the *run*, since all but a handful of findings are derived from the model and carry no file,
  so a severity set in a section narrower than the schema is an error (`scoped-severity`) rather than being quietly
  widened. `CliApplicationBuilder.Build` layers **`.editorconfig` < policy flag**, and the core prefers a setting by
  code over one by source — the more specific layer wins, as in every other tool that reads these keys. Formatting keys,
  when they land, are per-file and carry no such constraint.

`OptionBinding<T>` owns a single binding: an optional System.CommandLine `Option<T>`, an optional env var, and a parser.
Built fluently (`OptionBinding.Create<T>().FromOption("--x").FromEnvironmentVariable(EnvVar).WithDescription(...)`);
`.AllowMultipleArguments()`/`.Recursive()` configure the lazily-built, cached `.Option`. A binding with only
`.FromEnvironmentVariable` is environment-only (`.Option` throws). `Bind(cli, apply)` calls `apply` only when env or CLI
supplies a value; `TryGetValue`/`GetValueOrDefault(cli, …)` expose the same resolution for `--directory`/`--no-color`.
Env parsing is automatic (enums case-insensitively, strings by identity; pass a parser for other types).

## From config to a run

There is **no single superset config type**. Each command owns its own `*Configuration` model (`Commands/<Name>/`)
implementing `IBindable` (`void Bind(ProjectConfig, ParseResult)`) and composing only what it needs. A command's
`Bind` assigns the project slices directly and binds its **own** leaf flags through its command-local `*Options`
(`Provider = project.Provider; State = project.State; ApplyOptions.Scope.Bind(cli, s => Scope = s);`).

**`Commands/CommandRunner`** owns the preamble every configured command shares, so a command supplies only what varies:
its configuration type, its FluentValidation `*ConfigurationValidator` (optional — `validate`/`script hash` have
nothing to check), a `configure` lambda applying the resolved config to the `CliApplicationBuilder`, and the body. The
runner resolves the environment, calls `ConfigurationFactory.Load`, applies `configure`, calls `Build()`, and stops at
whichever step first fails — reporting its diagnostics and returning `ExitCodes.Error`. The body is handed a
`CommandContext<TConfiguration>` (the built `CliApplication`, the validated config, the `ParseResult`, the
environment). It also prints the environment banner, which `state pull` and `script hash` opt out of
(`announceEnvironment: false`) when stdout is a payload rather than a report. Commands that configure nothing
(`plan show`, `completion`, `init`, `new`) or that need the builder itself in the body (`doctor`, which reports plugin
failures via `TryConfigureDatabase`/`TryConfigureState` rather than stopping on them) deliberately bypass it.

The provider/backend slices are now **plain data**, not `IBindable`:

- **Provider** is a `PluginReference?` (`Configuration/Plugins/PluginReference`) — the resolved package id, pinned
  `version` (and the `RestoreVersion` range), label, and the `DATABASE` statement's attributes as a `PluginConfig` (which
  the plugin reads). `null` means offline. `PluginReference.Resolve` resolves it by matching the statement's label
  against the project's `PLUGIN` declarations (each names the package `source` and pins a `version`); there is **no
  built-in label→package map** — every plugin is declared explicitly. There is **no `ProviderConfig` slice**.
- **State** is a `StateConfig?` — a small union of `FileStateConfig? File` (the built-in local-file store) **xor**
  `PluginReference? Plugin` (every other backend, e.g. an `s3` label declared as `NSchema.Aws` via a `PLUGIN`). `null`
  means online-only. `file` is the lone built-in: no plugin, no `version`.

Presence is just a null check (`Provider is not null`, `State is not null`) — there is no `ConfiguredSectionCount` /
`IsConfigured`. Each command validator adds its **presence** rules on top: `apply` requires a provider
(`RuleFor(x => x.Provider).NotNull()`); `plan` requires a current-schema source — a provider (live) **or** a state store
(offline); `refresh`/`drift` require both. The desired schema is **not** a config concern (always the recursive project-file
glob), so no command validates its presence — the builder guards the zero-files case (an empty desired schema would read
as "drop everything").

`CliApplicationBuilder` wraps the core `NSchemaApplicationBuilder` and **trusts its validated inputs**.
`ConfigureDatabaseProvider(PluginReference?)` / `ConfigureBackendState(StateConfig?)` resolve the plugin and apply it;
`ConfigurePolicies(DestructiveActionPolicy?)` / `ConfigureConfirmation(bool)` take the command's flag-resolved values.
The file backend applies directly via the core's `UseFileStateStore`; a `null` provider/state is valid and means offline.

### Provider & backend plugins

A provider/backend is a NuGet package implementing a contract from `NSchema.Core`: **`INSchemaDatabasePlugin`**
(introspection + SQL generation) or **`INSchemaStatePlugin`** (state store), both exposing a `Label`, a
`GetScaffoldTemplate(ScaffoldContext)`, and a `Configure(NSchemaApplicationBuilder, PluginConfig)` returning a
`Result` (success, or aggregated errors — it does **not** throw, so `doctor` can report a misconfigured
plugin). `Configuration/Plugins/PluginLoader` turns a `PluginReference` into live instances: it synthesizes an
`EnableDynamicLoading` project, shells `dotnet publish` to materialise the pinned package's dependency closure into a
per-version cache under `~/.nschema/plugins` (whose on-disk layout is owned by `Configuration/Plugins/PluginCache` — the
loader delegates all path math to it), and loads it into an isolated `AssemblyLoadContext` that **defers
`NSchema.Core` + the framework assemblies to the host** (so contract types unify across the boundary) while isolating the
plugin's own deps (Npgsql, the AWS SDK, …). The host rejects a plugin whose referenced `NSchema.Core` **major** differs.
`CliApplicationBuilder` resolves the plugin by capability (the package's `INSchemaDatabasePlugin` /
`INSchemaStatePlugin`) and calls `Configure`; a failed result becomes a thrown error (the CLI is the single error
presenter). The provider's config vocabulary (statement attributes + its own env vars) and validation live **in the
plugin**, not the CLI.

**Adding a provider/backend is a new package**, not a CLI change: implement the contract, then declare it with a
`PLUGIN` statement naming the package via `source` and reference it by label from `DATABASE`/`STATE`.

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
run output (the diff, schema, SQL plan) is rendered by the CLI's `app.Reporter`, and live progress flows through the
core's `IProgress<OperationProgress>` (the CLI's `ConsoleProgress`). **Anything that reports goes through the reporter**
— the confirmation prompt included, since it is a console write that needed the format's stream and styling rather than
a fourth output source.

The exception is a **payload**, and the test is per-*write*, not per-command: *would `--format json` or `--quiet`
legitimately change these bytes?* If not, the bytes are a contract with a consumer (a shell, a file, `jq`, `xargs`)
rather than a rendering, and the command writes them raw — `completion <shell>` emits a shell script, `state pull`
the recorded payload verbatim (so pull/push round-trips byte-for-byte), `script hash <name>` a bare hash so
`$(nschema script hash x)` works, `format` the formatted source plus the compiler-style file list and
`path(line,col): message` errors an editor or CI problem matcher parses. Two commands split *within themselves* on
exactly this line — `script hash`'s listing form goes through `ReportScriptHashes` while its single-value form writes
raw; `completion install/uninstall` narrate while bare `completion <shell>` writes raw — and `announceEnvironment` is
computed per-invocation (`announceEnvironment: parseResult.GetValue(NameArgument) is null`) so the banner is suppressed
exactly when *that* invocation's stdout is a payload. A command whose whole surface is a payload **rejects the
presentation flags** rather than ignoring them silently (`format` errors on `--json`/`--format`/`--quiet`/`--verbose`).

`new`'s interactive scaffolding wizard (`Services/Prompting/ScaffoldPrompter`) also writes directly, on a different
ground: the reporter owns a prompt when the command has a run whose output format the prompt must fit into. Scaffolding
*is* the command — no plan, no diff, nothing `--json` could mean — so it stays out rather than growing the seam an
`Ask`/`Select` surface the JSON face could only throw from.

## Options layout

Options split by **source**, not just by command. A command's own CLI **flags** — `--scope`, `--auto-approve`,
`--destructive-actions`, etc. — are owned by its `Commands/<Name>/<Name>Options` class, one `OptionBinding` each, with the
description tailored to that command. Duplication of a flag across commands (both `apply` and `plan` declare their own
`--scope`) is the deliberate cost of per-command contextual help. (Provider/backend settings are **not** CLI bindings at
all — they live in the `DATABASE`/`STATE` statements and are read by the plugin; the CLI keeps only command flags plus the
harness-level options.) `Configuration/CommonOptions` holds the harness-level flags not bound to any command:
`Directory` and `NoColor`, read recursively at the root. Each `*Options` exposes `All` (its CLI bindings);
`*Command.Create` registers it with `command.Options.AddRange(<Name>Options.All)` (the `AddRange` extension lives in
`Extensions/CommandExtensions`), while the root command adds `CommonOptions.NoColor.Option` and
`CommonOptions.Directory.Option` recursively. Env-var **names** stay centralized in `Configuration/EnvironmentVariables`
as the auditable surface; the bindings reference those constants. (Provider-specific env vars — `NSCHEMA_<PROVIDER>_*` —
are owned by the plugins, not listed here.)

## Desired-schema files

The desired schema is every schema file found recursively under the project directory (the `--directory` root) — every
file without the `.env.` configuration marker — written in **NSchema DDL** (**NSQL**), the core's canonical,
SQL-flavoured schema serialization. A project file carries **either extension: `.nsql` or `.sql`**, both read the same
way (`Configuration/ProjectGlobs` owns the pair and is the only place either is spelled). `.nsql` names the language, so
it is what the CLI *writes* — `new` scaffolds `config.nsql` / `schemas/example.nsql` — while `.sql` keeps every project
written before the extension existed working, with no migration and no deprecation. Column types are canonical
compact strings (`bigint`, `text`, `varchar(255)`). There is no format, directory, or glob to configure. See
`README.md` for a worked example.

**Scripts** are declared **inline** in the DDL with the unified `SCRIPT '<name>' RUN [ALWAYS | ONCE] ON <event>
[(run_outside_transaction = true)] AS $$…$$;` statement (Core 4.4+). The event is a deployment bookend
(`PRE DEPLOYMENT` / `POST DEPLOYMENT`) or a structural change (`ADD COLUMN` / `ALTER COLUMN TYPE` / `ADD CONSTRAINT`
with a `schema.table.member` path — the data-migration form, spliced into the plan only when the matching change is
planned). `RUN ONCE` scripts are recorded in the state backend on a successful apply and skipped by later plans (skip
= `run-once` Info diagnostic; a changed body warns and stays skipped). Script names are unique project-wide. The
pre-4.4 spellings (`PRE|POST DEPLOYMENT 'name' AS $$…$$;`, `MIGRATION ['name'] FOR <trigger> <path> AS $$…$$;`) were
removed in 5.0. The CLI does **almost nothing special** with any of this: scripts ride the same project-file glob into the
core's parser, the core plans/executes/records them (the run-once manifest travels on `MigrationPlan` inside the plan
result and plan file, so apply needs no extra wiring), and the CLI's only additions are presentation — scripts are
folded into the plan's diff (run-once ones annotated `(run once)`; `runCondition` in `--json`), and the `run-once`
diagnostics render through the standard diagnostics table.

The **`script` command group** (`Commands/Script/`) manages the recorded ledger, all thin CLI over `app.State`
(`IDatabaseStateManager`): `script list` renders the recorded executions (a query — single bare array in `--json`),
`script taint <name>` removes an execution (read → `RemoveScript` → write), and `script untaint <name>` records a
run-once script as executed without running it — the name + body `Hash` come from the script's declaration, read
through `app.ProjectDefinition` (the expanded desired project), so no provider or plan is involved; untaint on an
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
