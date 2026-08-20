# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project (mostly) adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning policy

The `NSchema` CLI and the `NSchema.Core` engine ship from this repository with a **single shared version**: a release tags once and publishes both
packages, so `NSchema X.Y.Z` is always built on `NSchema.Core X.Y.Z`.

Breaking changes that are specific to the CLI surface (rather than the core API) may be signalled by a **minor version bump** rather than a major
one, and are called out explicitly in this changelog.

Entries up to and including 5.12.0 predate the repository merge and cover the CLI package alone; the engine's pre-merge history is preserved at
`src/NSchema.Core/CHANGELOG.md`.

## [Unreleased]

### Changed

- **Repository merge.** The `NSchema.Core` engine now lives in this repository and ships with the CLI from a shared tag, with one version covering
  both packages.

## [5.12.0] - 2026-08-17

## Added

- **NSQL extension.** Project files can now be written as `.nsql` files, which can be syntax highlighted using NSchema.Core's TextMate grammar.

## Changed

- **Default extension.** When scaffolding or importing project files, the default file extension is now `.nsql`

## [5.11.5] - 2026-08-13

All inherited from the latest NSchema.Core update.

### Fixed

- **Every remaining authored expression settles.** Column defaults and generated expressions, index and exclusion predicates, and a domain's checks and default are opaque SQL, rewritten by the engine, so a handwritten one would never match. All are now kept and declared like-for-like as they are for triggers, routines etc. An expression the database no longer reports is still drift, not a spelling to restore.
- **Renaming a type no longer retypes the columns declared against it.** The rename moved the type but left every reference naming the old one, so each column read as a retype.
- **Recreate is now correctly blocked by dependents.** Recreating a type that's in use now causes an error.
- **An identity that explicitly declares no options no longer differs from one that does so implicitly.** No options at all and a set of unstated ones now compare equal.
- **A sequence altered for one reason no longer restates the others.** The change carries the folded options, so a plan that changes the cache does not also restate a start it never asked to change.
- **Identity and sequence restarts now warn correctly.** Restarts are data hazards: restarting the counter, means duplicate values are issued, meaning inserts collide with what is already stored.

## [5.11.4] - 2026-08-12

### Fixed

- **Engines without comment support no-longer cause errors on comments.** Engines that don't support comments now correctly ignore them.

## [5.11.3] - 2026-08-12

### Fixed

- **Check constraints no longer cause permanent drift.** Check constraints are engine-native SQL, so it gets reformatted by the database. We now store both sides of a check constraint and only compare like to like.

## [5.11.2] - 2026-08-11

### Fixed

- **Migration plan file unable to be deserialized.** An accidental breaking change was introduced in NSchema.Core 5.9.1 that now been fixed.

## [5.11.1] - 2026-08-11

### Added

- **`plan --json` names the action behind each statement.** Every entry in `sql` carries the `action` it performs (`CreateTable`, `AddForeignKey`), so what a plan exercises can be read off it rather than inferred from the SQL text. Inherited from NSchema.Core.

### Fixed

- **An unreadable `.editorconfig` no longer crashes `init`, `new`, `plan show` or `completion install`/`uninstall`.**
- **`plan show` reports a corrupt plan file instead of crashing on it.** Inherited from NSchema.Core: a well-formed JSON object that is not a plan deserialized into nulls, and showing it died with a `NullReferenceException` in the reporter rather than naming the file.
- **`.editorconfig` severities now reach the findings that reading a project produces**, not only the engine's.

## [5.11.0] - 2026-08-11

### Added

- **Diagnostic severities can be set in `.editorconfig`.** `nschema_diagnostic.<code>.severity` configures one finding and `nschema_diagnostic_source.<source>.severity` every finding from a producer, taking Roslyn's severity words (`none`, `silent`, `suggestion`, `warning`, `error`, `default`). A `--destructive-actions` flag still wins over the file.
- **The diagnostics table names each finding's code**, which is what a severity is configured by.

### Fixed

- **`import` reports its diagnostics whether or not it succeeded.** They were shown only on failure, so a successful import had no way to tell you what it could not carry into the project.

## [5.10.0] - 2026-08-09

### Added

- **Plugins can be loaded from a path.** A `PLUGIN` statement declaring `path` loads the assembly directly, skipping the package restore and the shared cache entirely.

## [5.9.0] - 2026-08-09

### Added

**Clustering and XML indexes.** Added support for clustering, XML indexes and view indexes, all inherited from NSchema.Core 5.7.

## [5.8.0] - 2026-08-08

### Changed

- **`--quiet` now summarizes the run's output.** A quiet `plan` or `apply` reports one line per artifact (`Plan: 2 to add, 1 to change, 0 to destroy (5 statements).`) instead of the full diff and SQL.
- **`--quiet` drops Info diagnostics**. The run-once "already executed" advisories no longer repeat on every planned run.
- **`--quiet` suppresses the environment banner and secondary hint lines** (such as `lock acquire`'s release hint), which previously printed regardless.

### Fixed

- **`--json` no longer writes the confirmation prompt into the result stream.** An interactive `apply --json` wrote its summary and question to stdout as raw markup, breaking `nschema apply --json | jq`; the prompt now renders on stderr with the rest of the narration. The same fix keeps a piped `--format markdown` job summary free of prompt text. Without a terminal, the summary is reported as a `{"type":"log"}` event so a redirected stderr stays uniform NDJSON.
- **`--json` reports a `Detail` line at its own log level.** Secondary hint lines were emitted as `"level":"announcement"`, indistinguishable from top-level narration.
- **`format` rejects the presentation flags instead of ignoring them.** `nschema format --json` (and `--format`, `--quiet`, `--verbose`) because `format`'s output is the formatted code.
- The `NO_COLOR` environment variable and `--no-color` args should now be respected correctly.

## [5.7.1] - 2026-08-07

### Fixed

- **Environment overlays can declare schema again.** Database objects declared in `*.env.<name>.sql` will now join the project alongside the base files, restoring the v4 behavior.

## [5.7.0] - 2026-08-07

### Changed

- **`apply` and `destroy` capture the live schema before planning.** Before the plan step of `apply` and `destroy`, the state is now refreshed. This helps capture drift and also bootstraps the state before a first apply, which is particularly helpful when using the ephemeral store. Replaying a saved plan with `--plan-file` is unaffected, since its statements were fixed when the plan was written.

## [5.6.1] - 2026-08-07

### Fixed

- **Multi-line doc comments indent correctly.** Every line of a `---` doc comment on a column or setting now takes the member indent, rather than only the first.
- **A state payload with no captured schema is rejected.** Reading one now fails as an unreadable payload, instead of throwing an NRE.
- **Lockfiles are updated additively.** Pinning package version now no-longer clobbers packages that weren't in the updated list, so initializing plugins for one environment won't remove pins for a different environment.

## [5.6.0] - 2026-08-07

### Changed

- **Full dependency graph support.** Updated to NSchema.Core 5.6.0, which refactors the linearizer to be built entirely from the dependency graph.

### Fixed

- **Imported SQL Server projects read back.** Inherits NSchema.Core 5.5.1: multi-statement routine definitions and view bodies survive the import round trip via dollar-quoted bodies, and trailing line comments no longer swallow the closing tokens.

## [5.5.0] - 2026-08-06

### Fixed

- **Engine-native SQL bodies no longer show as permanent drift.** Inherited from NSchema.Core 5.5.0, hand-written and database-written provider-native SQL (things like view bodies or trigger definitions) are now stored in the state so they can be diffed like-for-like.

## [5.4.0] - 2026-08-04

### Added

- **Aggregate support.** `CREATE AGGREGATE` is part of the language (inherited from NSchema.Core 5.4.0).
- **Plugin restore honors your project's NuGet configuration.** Plugin resolution and restore now run under the project directory, so a `NuGet.Config` beside the project applies as expected.

### Fixed

- **Functions that call each other now apply in the right order.** Inherited from NSchema.Core 5.4: a routine's definition is scanned for the routines it calls and the objects it reads, and creates are ordered so a callee precedes its caller.
- **Plugin restore works inside a repository using central package management.** Only your NuGet configuration reaches the restore now; a `Directory.Packages.props` (or `Directory.Build.props`) further up the tree no longer breaks it with NU1008.

## [5.3.0] - 2026-08-03

### Changed

- **A create is a create.** Built on NSchema.Core 5.3: a plan that believes it is creating a routine or view now renders a plain `CREATE`, so colliding with an object the plan didn't know about fails loudly instead of being silently overwritten; the in-place forms render only when the plan knows it is replacing.

### Fixed

- **Rebuilding a schema whose functions reference its tables.** Routines are now created after the tables they may reference, so applying an imported schema with SQL-language functions (e.g. Pagila) no longer fails with a missing relation.

## [5.2.0] - 2026-08-03

### Changed

- **Plans verify type references.** Inherited from NSchema.Core 5.2.0, a plan now checks that every type the project references will exist once it applies.

### Fixed

- **Schema-qualified engine types no longer block planning.** Applying an imported project that referenced engine types (e.g. Postgres's `pg_catalog.tsvector`) previously failed with an error demanding a declaration nobody could write.

## [5.1.0] - 2026-08-02

### Changed

- **Implicit schemas are now ignored.** Schemas owned by the database (`dbo`, `public`, `main`, etc.) will now be ignored during import and planning.

### Fixed

- **Plugin dependencies are not shared correctly.** All plugin dependencies will now be correctly loaded from their own dependency closure where the CLI does not have its own version of an assembly.

## [5.0.1] - 2026-08-01

### Fixed

- **`--destructive-actions` argument is ignored.** Policy enforcement overrides like `--destructive-actions` and `--data-hazards` weren't being respected due to a bug in Core. This is fixed after updating to 5.0.1.

## [5.0.0] - 2026-08-01

v5.0 moves the CLI onto `NSchema.Core 5.0`, whose rearchitecture reshapes configuration, plugins, and planning. Changes below are relative to 4.5.1.

### Added

- **`--scope` takes an object, not just a schema.** A value is an address — `--scope app` for a whole schema, `--scope app.orders` for one object. Read under the NSQL identifier rules, so a quoted segment can carry dots and spaces (`--scope '"my.schema"."Order Details"'`).
- **`--ephemeral`** on `plan`, `apply`, and `destroy` runs against an in-memory state store discarded when the command exits, standing in for a configured `STATE` store — for CI pipelines that bootstrap disposable databases. Run-once script history does not persist across runs in this mode.
- **A lockfile (`nschema.lock`) pins declared plugin versions to concrete versions.** `init` resolves each `PLUGIN` — a range to the highest available version, an exact pin to itself — and records it; later commands read the pin, so a plugin without a lockfile entry is an error that points to `init`.
- **`plugin update [<label>]`** re-resolves ranges to their highest available version and rewrites the lockfile — every plugin, or a single one by label.
- **`plugin outdated`** shows each plugin's pinned version against the newest its range allows (what `update` would install) and the newest available for this engine.
- **`new` asks the plugins what it needs to know.** A database or state plugin can declare the questions its configuration needs, composing the answers into the statement it writes.
- **`--set <key>=<value>`** on `new` answers a question up front, so a scripted run never blocks. Repeatable.

### Changed

- **Environments select configuration, not schema.** `--environment` layers the environment's files over the base configuration; schema files are no longer overlaid per environment. An overlay merges the `DATABASE`, `STATE`, or `ENGINE` statement attributes.
- **`DATABASE` and `STATE` replace `PROVIDER` and `BACKEND`.** Each names the thing it configures. The built-in local-file store is `STATE file ( path = '…' );`.
- **`PLUGIN` declares plugin dependencies.** `PLUGIN <label> ( source = '…', version = '…' );` names the package and pins its version; `DATABASE`/`STATE` reference the label. The built-in label-to-package map is gone — every plugin is declared explicitly, and `version`/`source` no longer ride the configuring statement. A `version` may be an exact pin or a NuGet-style range (`[5.0,6.0)`).
- **`ENGINE ( version = '…' );` asserts the engine version.** A project can require an engine version range; a mismatch fails with a pointer to `dotnet tool update`.
- **`new` authors the `ENGINE` assertion.** A scaffolded project pins the engine to the CLI's current major (e.g. `ENGINE ( version = '[5.0,6.0)' );`), so it fails fast on an incompatible tool.
- **`new` runs `init` afterwards**, resolving and locking the plugins it declared so the project is ready to `plan` immediately. Pass `--no-init` to skip it for an offline or edit-first workflow.
- **Planning always diffs recorded state against the project**, so `plan` (and a fresh `apply`) now require both a database and a state store. Planning against the live database is no longer available; use `refresh` to capture the live schema first.
- **`destroy` reads the managed schema from the recorded state.** The fallback to the working-directory schema when no store was configured is gone, and a state store is now required.
- **Plan output folds scripts into the diff.** Deployment and change-event scripts are first-class parts of the diff, shown in the plan tree (and carried on the `diff` object in `--json`); the separate pre/post-deployment and data-migration sections are gone.
- **Policy-blocked plans still render.** A plan blocked by policy shows the complete diff and SQL alongside the blocking diagnostics; error severity is what stops an apply.
- **`--destructive-actions` accepts `Ignore`** alongside `Error`, `Warn`, and `Allow`.
- **Abbreviated commands are spelled out.** `fmt` is `format`, `db` is `database`, and `scaffold` is `new`. The old names are gone rather than aliased, so a script still using one fails loudly instead of drifting.
- **`apply` re-runs policies.** The policy flags now apply to `apply --plan-file` too, re-checking the saved plan before executing it.
- **`script hash`, `script taint`, and `script untaint` operate on deployment scripts.** A template-scoped script is addressed as `schema.name`, as `script hash` lists it.
- **Errors you can fix are reported as diagnostics; anything else is considered a a bug.** A broken configuration file, an unresolvable plugin, an unreachable database — each renders in the diagnostics table, sourced by the file and line or the plugin label that caused it. Anything else reaching the top level is a defect in NSchema: it names the exception type, links the issue tracker, and prints a stack trace unconditionally, so a report needs no re-run with a flag. Messages carry the inner-exception chain, so a cause like `Failed to connect to 127.0.0.1:5432 -> Connection refused` keeps the part that matters.
- **`--json` distinguishes the two.** An expected failure emits a `{"type":"diagnostics"}` event; an internal error's `{"type":"error"}` event gains `exception` and `stack`, so a consumer can tell "your project is wrong" from "NSchema is broken" by a null check.

### Fixed

- **Editing a `PLUGIN` version now works correctly.** `init` preferred the lockfile's pin unconditionally, so changing a declared version and re-running `init` silently kept restoring the old one.
- **An incompatible plugin doesn't crash the CLI.** This CLI will now give a proper explanation of the problem instead.
- **`plugin update` exits non-zero when a restore fails**, rather than reporting the problem and returning `0`.
- Plugin loading now resolves a plugin's native libraries (e.g. SQLite's `e_sqlite3`) from its restored dependency closure.
- **`new` names the right environment variable.** It pointed at a per-provider variable (`NSCHEMA_POSTGRES_CONNECTION_STRING`) that no longer has any effect; it now names `NSCHEMA_DATABASE_CONNECTION_STRING`.
- **Contradictory flags fail fast.** `--quiet` with `--verbose`, or `--json` with a non-json `--format`, are now rejected while parsing.
- **A failed command always exits non-zero.** `lock release`, `init`, `new`, and the `plugin` commands reported failures but still exited `0`.
- **`state show` reports an error when no state has been recorded yet** instead of failing on a missing source.

## [4.5.1] - 2026-07-10

### Changed

- A `RUN ONCE` script that has already been run no-longer produces an informational diagnostic.

## [4.5.0] - 2026-07-10

### Added

- **`refresh --force`** refresh fails if it finds an unreadable state payload, instead of silently overwriting it; `--force` replaces it, resetting the script ledger.
- **`state pull [file]`** to pull the raw recorded state payload out of the configured backend. Writes to a file or stdout.
- **`state push <file>`** to push the raw recorded state payload into the configured backend. Push takes the state lock (`--no-lock` to skip).
- **`script` command group** to manage the scripts recorded in the state:
  - `script list` shows the recorded scripts (name, execution time, body hash); `--json` emits them as a single array.
  - `script hash [name]` computes the body hash of the project's script declarations, bare on stdout for one script, or a listing of all of them, for hand-editing pulled state.
  - `script taint <name>` removes a script's record, so it runs again on the next apply.
  - `script untaint <name>` records a script as executed without running it, using the body hash from the script's declaration. Taint and untaint take the state lock (`--no-lock` to skip).

## [4.4.0] - 2026-07-10

### Added

- **Unified `SCRIPT` statement** (via `NSchema.Core 4.4.0`). `SCRIPT '<name>' RUN [ALWAYS | ONCE] ON <event> AS $$…$$;` is the new canonical form of deployment scripts and data migrations: the event is `PRE DEPLOYMENT`, `POST DEPLOYMENT`, or a structural change (`ADD COLUMN` / `ALTER COLUMN TYPE` / `ADD CONSTRAINT` with a target path).
- **Run-once scripts.** A `RUN ONCE` script is recorded in the state backend on a successful apply and skipped by later plans; a recorded script whose body has since changed stays skipped and warns. Plan output marks run-once scripts in the pre/post-deployment sections (`(run once)`; `runCondition` in `--json`). Recording requires a state backend — planning without one warns.
- **Scripts in schema templates.** Both script kinds can be declared inside a `TEMPLATE … BEGIN … END;` body and instantiate once per applied schema, with the `{schema}` token substituted in the name and the SQL.

### Changed

- Script names must be unique across the project (they identify scripts in diagnostics and run-once tracking); a template-declared script applied to multiple schemas can include `{schema}` in its name.

### Deprecated

- The `PRE|POST DEPLOYMENT '<name>' AS $$…$$;` and `MIGRATION ['name'] FOR <trigger> <path> AS $$…$$;` forms still work, but plan/apply/validate now surface a `deprecations` warning naming the `SCRIPT` replacement. They will be removed in NSchema 5.0.

## [4.3.0] - 2026-07-09

### Added

- **Data migrations.** A `MIGRATION ['name'] FOR <trigger> <schema>.<table>.<member> AS $$…$$;` block (via `NSchema.Core 4.3.0`) attaches raw SQL to an `ADD COLUMN`, `ALTER COLUMN TYPE`, or `ADD CONSTRAINT` change and runs only when that change is in the plan. A required column add with a matching block is applied as add-nullable → backfill → `SET NOT NULL`, a matching block silences the corresponding data-hazard warning, and a block matching nothing is reported as safe to delete. The plan output gains a "Data migrations" section (`dataMigrations` in `--json`). Executing a plan with a matched block requires a provider plugin at 4.3 or later.

### Changed

- The `import` command now writes the per-schema header to `<schema>/schema.sql` instead of `<schema>.sql`.

### Fixed

- The `nschema lock release` command suggested by `lock status` and `lock acquire` now carries the `--environment` and `--directory` arguments of the current invocation.
- The diff now shows an added or removed column's default expression and identity marker.
- DDL syntax errors now name the file the error was found in, alongside the existing line and column.
- The `import` command no longer repeats the `CREATE SCHEMA` statement in every object file; only the per-schema header declares the schema.

## [4.2.0] - 2026-07-09

### Added

- **Data-hazard detection.** `plan` and `apply` (via `NSchema.Core 4.2.0`) now flag changes that are valid against the schema but can fail on the data already in a table.

## [4.1.0] - 2026-07-08

### Added

- Updated to `NSchema.Core 4.1.0` which adds support for schema and table templates.

## [4.0.1] - 2026-07-07

### Fixed

- Updated to `NSchema.Core 4.0.1` which fixes several issues to do with action ordering when objects are renamed.

## [4.0.0] - 2026-07-01

Version 4.0.0 changes the provider and backend model to function as plugins resolved through the NuGet package manager.

### Added

- **Third-party providers and backends.** A `PROVIDER` / `BACKEND` block can name any plugin package with a `source` attribute.
- **`nschema init` now restores plugins.** `init` now pre-fetches the provider and backend plugins pinned in your config. Operations restore implicitly
  on first use; `init` just does it up front so the first real command is fast.
- **`--no-init` flag.** Skips the implicit plugin restore and requires the plugins to be cached already.
- **`lock` command group.** `nschema lock status` / `lock acquire` / `lock release` inspect, manually hold, and release the state lock. `lock acquire`
  holds a lock that outlives the command (for out-of-band checks before a migration), with an optional `--ttl` (e.g. `30m`) and `--reason`, `lock status`
  surfaces any information about the currently held lock. `lock release` requires the lock id by default (refusing if it no longer matches the held lock),
  with `--force` to release whatever lock is held without naming it.
- **`--no-lock` flag** on `apply`, `refresh`, and `destroy`. Runs without taking the state lock.
- **`nschema state show <file>`** renders a state file on disk directly, without a configured backend.
- **`nschema db show`** renders the live database schema, read directly from the database via the provider — the online counterpart to `state show`.
- **`plugin` command group.** `nschema plugin list` shows the provider and backend plugins your project pins and whether each is restored;
  `plugin show <label>` prints one plugin's detail (package, pinned version, cache status). `plugin cache list` /
  `plugin cache remove <package> [version]` / `plugin cache clear` inspect and prune the shared plugin cache at `~/.nschema/plugins`.
- **`--format` option** (`text` | `json` | `markdown`), selecting the output format for any command. `--json` is now shorthand for `--format json`.
- **Markdown output.** `--format markdown` renders the plan, SQL, and schema as Markdown for a PR comment or a CI job summary.

### Changed

- **Providers and backends are now plugins.** They ship as separate NuGet packages instead of being bundled with the tool; `nschema` restores the one
  pinned in your config on first use (it shells out to the .NET SDK to do so). The local-file state backend remains built in.
- **Scaffolding moved from `init` to `nschema scaffold`.** Creating a starter project is now `nschema scaffold` (`init` became the restore command above).
  Its `PROVIDER` / `BACKEND` config blocks and the sample schema are rendered by the plugins themselves.
- **`PROVIDER` / `BACKEND` blocks now require a pinned `version`** (the plugin package version); the built-in `file` backend is the exception. A first-party
  label (`postgres`, `sqlite`, `sqlserver`, `s3`) still resolves to its package automatically.
- **A `PROVIDER` block is now required to select a provider.** `NSCHEMA_POSTGRES_CONNECTION_STRING` and the other connection-string variables no longer
  name the provider on their own — they still override the connection string set in the block.
- **`doctor` reports plugin problems as diagnostics.** A provider or backend that fails to restore or configure is now reported by `doctor` as a
  health-check finding (every such problem at once) instead of aborting on the first.
- **Lock commands grouped under `lock`.** `lock-status` → `nschema lock status`; `force-unlock` → `nschema lock release`, whose prompt is now skipped with
  `--auto-approve`/`-y` (consistent with `apply`/`destroy`) instead of `--force`. The lock-id safety check is unchanged.
- **`show` split by what it shows.** The recorded state is now `nschema state show` (offline; the `state` noun group will grow `pull`/`push`/`move`), and a
  saved plan is `nschema plan show <file>`. The top-level `show` command is gone.
- **`completion install` / `completion uninstall` subcommands** replace the `--install-autocomplete` / `--uninstall-autocomplete` flags. `nschema completion <shell>`
  still prints the script.
- Built on `NSchema.Core 4.0.0` and the 4.0 provider/backend packages.

### Fixed

- Running `nschema --help` in a busy directory like root would cause a performance slowdown due to the `--environment` arg autocomplete recursively scanning
  all the files in the directory. This has been fixed by removing autocomplete.
- **Torn reads of the local state file.** The built-in file state store now writes to a temporary sibling file and
    atomically renames it into place, so a command reading the recorded state while another run writes it.

### Removed

- **The `NSCHEMA` config block.** `destructive_action` moved to the `--destructive-actions` flag / the `NSCHEMA_DESTRUCTIVE_ACTION_POLICY` environment
  variable; `dialect` and `transaction_mode` (never wired in) are gone. An `NSCHEMA` block is now rejected as an unknown configuration block.
- **The top-level `show`, `lock-status`, and `force-unlock` commands**, replaced by `state show` / `plan show` and the `lock` group above. The `show --online`
  live-schema view is now `nschema db show` (a `db` noun group) rather than a mode flag.

## [3.4.0] - 2026-06-25

### Added

- **`doctor` command.** A new `nschema doctor` command runs read-only health checks against your declared infrastructure, including database connectivity,
  state-store reachability, and the state lock. It exits `1` when any configured check fails, for gating in CI.
- **`force-unlock <lock-id>`.** `force-unlock` now accepts the lock id (shown in the blocked operation's error) and refuses if it no longer matches the held
  lock — a safety guard against breaking a lock that changed under you. Bare `force-unlock` still releases whatever lock is held. Requires `NSchema.Core 3.4.0`
  and `NSchema.Aws 3.2.0`.
- **`lock-status` command.** A new `nschema lock-status` reports whether the state store is locked. Supports `--json` for structured output and
  `--detailed-exitcode` (exit `2` when locked) for CI gating.

## [3.3.0] - 2026-06-25

### Added

- **Init options.** The `init` command now accepts `--database` (`postgres`, `sqlite`, `sqlserver`) and `--backend` (`file`, `s3`) options to scaffold
  configuration for a specific provider/backend combination.
- **S3-compatible state stores.** The `BACKEND s3` block accepts a `force_path_style` attribute for S3-compatible stores (such as MinIO) that require
  path-style addressing. The endpoint, region, and credentials continue to come from the ambient AWS configuration (`AWS_ENDPOINT_URL_S3`, `AWS_REGION`,
  and the credential chain).
- Updated to `NSchema.Core 3.3.0` and the latest provider packages.

### Fixed

- **`destroy` now tears down SQL Server and SQLite projects.** Teardown previously failed for these providers because SQL Server's `DROP SCHEMA` does
  not cascade, and SQLite cannot drop its implicit `main` schema. The migration engine now drops a schema's contained objects before the schema itself.
- **DDL formatting (`fmt`).** Fixed two formatting bugs: comments following the last attribute in a block were flattened onto a single line, and a
  blank line between a leading comment and its statement was removed.

## [3.2.0] - 2026-06-22

### Added

- **Short option aliases.** Common flags now have single-character forms: `-C` (`--directory`), `-e` (`--environment`), `-v` (`--verbose`), `-q` (`--quiet`),
  `-s` (`--scope`), `-y` (`--auto-approve`), `-f` (`--force`), `-o` (`--out` / `--out-dir`), and `-p` (`--plan-file`).
- **Tab-completion for environment names.** `--environment <TAB>` now completes the environment names discovered from the project's `*.env.<name>.sql` files.

## [3.1.0] - 2026-06-21

### Added

- **SQLite Support.** Use:
  ```sql
  PROVIDER sqlite (
    connection_string = 'Data Source=app.db'
  )
  ```
  Connection string may also be supplied separately via the `NSCHEMA_SQLITE_CONNECTION_STRING` environment variable.
- **SQL Server Support.** Use:
  ```sql
  PROVIDER sqlserver (
    connection_string = 'Server=localhost;Database=app'
  )
  ```
  Credentials and command timeout may also be supplied separately, via the `username` / `password` /`command_timeout` block attributes or the
  `NSCHEMA_SQLSERVER_CONNECTION_STRING` / `NSCHEMA_SQLSERVER_USERNAME` / `NSCHEMA_SQLSERVER_PASSWORD` environment variables.

## [3.0.0] - 2026-06-20

Initial release of the NSchema CLI. `dotnet tool install -g nschema`

See https://nschema.dev for full documentation.

[5.12.0]: https://github.com/nschema-org/NSchema/compare/v5.11.5...v5.12.0
[5.11.5]: https://github.com/nschema-org/NSchema/compare/v5.11.4...v5.11.5
[5.11.4]: https://github.com/nschema-org/NSchema/compare/v5.11.3...v5.11.4
[5.11.3]: https://github.com/nschema-org/NSchema/compare/v5.11.2...v5.11.3
[5.11.2]: https://github.com/nschema-org/NSchema/compare/v5.11.1...v5.11.2
[5.11.1]: https://github.com/nschema-org/NSchema/compare/v5.11.0...v5.11.1
[5.11.0]: https://github.com/nschema-org/NSchema/compare/v5.10.0...v5.11.0
[5.10.0]: https://github.com/nschema-org/NSchema/compare/v5.9.0...v5.10.0
[5.9.0]: https://github.com/nschema-org/NSchema/compare/v5.8.0...v5.9.0
[5.8.0]: https://github.com/nschema-org/NSchema/compare/v5.7.1...v5.8.0
[5.7.1]: https://github.com/nschema-org/NSchema/compare/v5.7.0...v5.7.1
[5.7.0]: https://github.com/nschema-org/NSchema/compare/v5.6.1...v5.7.0
[5.6.1]: https://github.com/nschema-org/NSchema/compare/v5.6.0...v5.6.1
[5.6.0]: https://github.com/nschema-org/NSchema/compare/v5.5.0...v5.6.0
[5.5.0]: https://github.com/nschema-org/NSchema/compare/v5.4.0...v5.5.0
[5.4.0]: https://github.com/nschema-org/NSchema/compare/v5.3.0...v5.4.0
[5.3.0]: https://github.com/nschema-org/NSchema/compare/v5.2.0...v5.3.0
[5.2.0]: https://github.com/nschema-org/NSchema/compare/v5.1.0...v5.2.0
[5.1.0]: https://github.com/nschema-org/NSchema/compare/v5.0.1...v5.1.0
[5.0.1]: https://github.com/nschema-org/NSchema/compare/v5.0.0...v5.0.1
[5.0.0]: https://github.com/nschema-org/NSchema/compare/v4.5.1...v5.0.0
[4.5.1]: https://github.com/nschema-org/NSchema/compare/v4.5.0...v4.5.1
[4.5.0]: https://github.com/nschema-org/NSchema/compare/v4.4.0...v4.5.0
[4.4.0]: https://github.com/nschema-org/NSchema/compare/v4.3.0...v4.4.0
[4.3.0]: https://github.com/nschema-org/NSchema/compare/v4.2.0...v4.3.0
[4.2.0]: https://github.com/nschema-org/NSchema/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/nschema-org/NSchema/compare/v4.0.1...v4.1.0
[4.0.1]: https://github.com/nschema-org/NSchema/compare/v4.0.0...v4.0.1
[4.0.0]: https://github.com/nschema-org/NSchema/compare/v3.4.0...v4.0.0
[3.4.0]: https://github.com/nschema-org/NSchema/compare/v3.3.0...v3.4.0
[3.3.0]: https://github.com/nschema-org/NSchema/compare/v3.2.0...v3.3.0
[3.2.0]: https://github.com/nschema-org/NSchema/compare/v3.1.0...v3.2.0
[3.1.0]: https://github.com/nschema-org/NSchema/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/nschema-org/NSchema/releases/tag/v3.0.0
