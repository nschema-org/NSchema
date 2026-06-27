# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project (mostly) adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning policy

This package uses **lockstep major versioning** with the core NSchema package: `NSchema X.*.*` is built on `NSchema.Core X.*.*`, so version
compatibility is always clear.

As a consequence, breaking changes that are specific to this provider (rather than the core API) are signalled by a **minor version bump** rather than
a major one, and called out explicitly in this changelog.

## [Unreleased]

Version 4.0.0 changes the provider and backend model to function as plugins resolved through the NuGet package manager.

### Added

- **Third-party providers and backends.** A `PROVIDER` / `BACKEND` block can name any plugin package with a `source` attribute.

### Changed

- **Providers and backends are now plugins.** They ship as separate NuGet packages instead of being bundled with the tool; `nschema` restores the one
  pinned in your config on first use (it shells out to the .NET SDK to do so). The local-file state backend remains built in.
- **`PROVIDER` / `BACKEND` blocks now require a pinned `version`** (the plugin package version); the built-in `file` backend is the exception. A first-party
  label (`postgres`, `sqlite`, `sqlserver`, `s3`) still resolves to its package automatically.
- **A `PROVIDER` block is now required to select a provider.** `NSCHEMA_POSTGRES_CONNECTION_STRING` and the other connection-string variables no longer
  name the provider on their own — they still override the connection string set in the block.
- Built on `NSchema.Core 4.0.0` and the 4.0 provider/backend packages.

### Removed

- **The `NSCHEMA` config block.** `destructive_action` moved to the `--destructive-actions` flag / the `NSCHEMA_DESTRUCTIVE_ACTION_POLICY` environment
  variable; `dialect` and `transaction_mode` (never wired in) are gone. An `NSCHEMA` block is now rejected as an unknown configuration block.

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

[Unreleased]: https://github.com/nschema-org/NSchema/compare/v3.4.0...HEAD
[3.4.0]: https://github.com/nschema-org/NSchema/compare/v3.3.0...v3.4.0
[3.3.0]: https://github.com/nschema-org/NSchema/compare/v3.2.0...v3.3.0
[3.2.0]: https://github.com/nschema-org/NSchema/compare/v3.1.0...v3.2.0
[3.1.0]: https://github.com/nschema-org/NSchema/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/nschema-org/NSchema/releases/tag/v3.0.0
