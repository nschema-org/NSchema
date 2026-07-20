# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project (mostly) adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning policy

This package uses **lockstep major versioning** with the core NSchema package: `NSchema X.*.*` is built on `NSchema.Core X.*.*`, so version
compatibility is always clear.

As a consequence, breaking changes that are specific to this provider (rather than the core API) are signalled by a **minor version bump** rather than
a major one, and called out explicitly in this changelog.

## [Unreleased]

v5.0 moves the CLI onto `NSchema.Core 5.0`, whose rearchitecture reshapes configuration, plugins, and planning. Changes below are relative to 4.5.1.

### Added

- **`--ephemeral`** on `plan`, `apply`, and `destroy` runs against an in-memory state store discarded when the command exits, standing in for a configured `STATE` store — for CI pipelines that bootstrap disposable databases. Run-once script history does not persist across runs in this mode.

### Changed

- **Configuration lives in configuration files.** The `.env.` marker in a file name makes it configuration: `<any>.env.sql` loads for every environment, and `<any>.env.<name>.sql` loads only when that environment is selected (multiple files of either kind may exist). Every other `.sql` file holds only schema DDL, and configuration statements no longer parse there.
- **Environments select configuration, not schema.** `--environment` layers the environment's configuration files over the base; schema files are no longer overlaid per environment.
- **`DATABASE` and `STATE` replace `PROVIDER` and `BACKEND`.** Each names the thing it configures. The built-in local-file store is `STATE file ( path = '…' );`.
- **`PLUGIN` declares plugin dependencies.** `PLUGIN <label> ( source = '…', version = '…' );` names the package and pins its version; `DATABASE`/`STATE` reference the label. The built-in label-to-package map is gone — every plugin is declared explicitly, and `version`/`source` no longer ride the configuring statement. A `version` may be an exact pin or a NuGet-style range (`[5.0,6.0)`).
- **`ENGINE ( version = '…' );` asserts the engine version.** A project can require an engine version range; a mismatch fails with a pointer to `dotnet tool update`.
- **Planning always diffs recorded state against the project**, so `plan` (and a fresh `apply`) now require both a database and a state store. Planning against the live database is no longer available; use `refresh` to capture the live schema first.
- **`destroy` reads the managed schema from the recorded state.** The fallback to the working-directory schema when no store was configured is gone, and a state store is now required.
- **Plan output folds scripts into the diff.** Deployment and change-event scripts are first-class parts of the diff, shown in the plan tree (and carried on the `diff` object in `--json`); the separate pre/post-deployment and data-migration sections are gone.
- **Policy-blocked plans still render.** A plan blocked by policy shows the complete diff and SQL alongside the blocking diagnostics; error severity is what stops an apply.
- **`--destructive-actions` accepts `Ignore`** alongside `Error`, `Warn`, and `Allow`.
- **`apply` re-runs policies.** The policy flags now apply to `apply --plan-file` too, re-checking the saved plan before executing it.
- **`script hash`, `script taint`, and `script untaint` operate on deployment scripts.** A template-scoped script is addressed as `schema.name`, as `script hash` lists it.
- **`state show` reports an error when no state has been recorded yet** instead of failing on a missing source.

### Fixed

- Plugin loading now resolves a plugin's native libraries (e.g. SQLite's `e_sqlite3`) from its restored dependency closure.

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

- **Init options.** The `init` command now accepts `--provider` (`postgres`, `sqlite`, `sqlserver`) and `--backend` (`file`, `s3`) options to scaffold
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

[Unreleased]: https://github.com/nschema-org/NSchema/compare/v4.3.0...HEAD
[4.3.0]: https://github.com/nschema-org/NSchema/compare/v4.2.0...v4.3.0
[4.2.0]: https://github.com/nschema-org/NSchema/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/nschema-org/NSchema/compare/v4.0.0...v4.1.0
[4.0.1]: https://github.com/nschema-org/NSchema/compare/v4.0.0...v4.0.1
[4.0.0]: https://github.com/nschema-org/NSchema/compare/v3.4.0...v4.0.0
[3.4.0]: https://github.com/nschema-org/NSchema/compare/v3.3.0...v3.4.0
[3.3.0]: https://github.com/nschema-org/NSchema/compare/v3.2.0...v3.3.0
[3.2.0]: https://github.com/nschema-org/NSchema/compare/v3.1.0...v3.2.0
[3.1.0]: https://github.com/nschema-org/NSchema/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/nschema-org/NSchema/releases/tag/v3.0.0
