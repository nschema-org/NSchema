# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project (mostly) adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning policy

This package uses **lockstep major versioning** with the core NSchema package: `NSchema X.*.*` is built on `NSchema.Core X.*.*`, so version
compatibility is always clear.

As a consequence, breaking changes that are specific to this provider (rather than the core API) are signalled by a **minor version bump** rather than
a major one, and called out explicitly in this changelog.

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

[3.1.0]: https://github.com/nschema-org/NSchema/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/nschema-org/NSchema/releases/tag/v3.0.0
