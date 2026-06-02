# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project (mostly) adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning policy

This package uses **lockstep major versioning** with the core NSchema package: `NSchema.Cli X.*.*` is built on `NSchema X.*.*`, so version compatibility is always clear.

As a consequence, breaking changes that are specific to this provider (rather than the core API) are signalled by a **minor version bump** rather than a major one, and called out explicitly in this changelog.

## [Unreleased]

Initial release of the NSchema CLI. `dotnet tool install -g NSchema.Cli`

### Added

- `init`, `plan`, `apply`, and `refresh` commands for scaffolding a project and previewing, applying, and snapshotting schema migrations.
- Schema support for JSON and YAML files.
- Provider support for Postgres.
- Backend store support for files and Amazon S3.
- Configuration from multiple sources, including `nschema.json`, environment variables, and CLI args.
- `--scope` to limit a migration to specific database schemas.
- `--destructive-actions` to control the policy for destructive changes `<error|warn|allow>`.
- `--auto-approve` to skip the confirmation prompt on `apply`.
