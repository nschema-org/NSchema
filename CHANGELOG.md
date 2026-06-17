# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project (mostly) adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning policy

This package uses **lockstep major versioning** with the core NSchema package: `NSchema X.*.*` is built on `NSchema.Core X.*.*`, so version compatibility is always clear.

As a consequence, breaking changes that are specific to this provider (rather than the core API) are signalled by a **minor version bump** rather than a major one, and called out explicitly in this changelog.

## [Unreleased]

Initial release of the NSchema CLI. `dotnet tool install -g nschema`

### Added

- `init`, `plan`, `apply`, `refresh`, `import`, `destroy` and `validate` commands.
- Schema support for JSON and YAML files.
- Provider support for Postgres.
- Backend store support for files and Amazon S3.
- Project configuration declared in the `.sql` files as `NSCHEMA` / `PROVIDER` / `BACKEND` config blocks, overridable by environment variables and CLI args (config blocks < env < CLI).
- `--scope` to limit a migration to specific database schemas.
- `--destructive-actions` to control the policy for destructive changes `<error|warn|allow>`.
- `--auto-approve` to skip the confirmation prompt on `apply` and `destroy`.
