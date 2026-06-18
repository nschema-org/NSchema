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
- Environments. `--environment <name>` (or `NSCHEMA_ENVIRONMENT`) layers per-environment overlay files named `<name>.env.<environment>.sql` over the base configuration: their `NSCHEMA` / `PROVIDER` / `BACKEND` blocks override the base per slice (so an overlay `BACKEND s3` cleanly replaces a base `BACKEND file`), and their schema is added on top. Overlays are excluded from the base schema and from deployment scripts; selecting an environment with no matching files is an error. Selection is per-invocation only (never persisted).
- Machine-readable output. `--json` emits newline-delimited JSON (NDJSON) instead of formatted text — one typed object per artifact on stdout (`{"type":"diff",…}`, `{"type":"sqlPlan",…}`, `{"type":"schema",…}`, `{"type":"diagnostics",…}`), with progress on stderr, so a run can be piped to `jq` (e.g. `nschema plan --json | jq -c 'select(.type=="diff")'`). Works on every command.
- Detailed exit codes. `plan` and `drift` now return `0` when there are no changes / no drift and `2` when there are (errors remain `1`), so CI can gate on "does this change the schema?" / "has the database drifted?" without parsing output — the analogue of Terraform's `plan -detailed-exitcode`.
- Shell tab-completion. `nschema completion <bash|zsh|fish|pwsh>` prints a completion script for your shell (e.g. `source <(nschema completion bash)`). Completions are dynamic — subcommands, options, and option values come straight from the command tree via System.CommandLine's `[suggest]` directive, so they never drift from the CLI — and self-contained (no `dotnet-suggest` or other external tool required).
- `show <planfile>`. `show` now takes an optional saved plan file (from `plan --out`): given one, it renders that plan's diff, plan, and SQL — the same view the plan produced — instead of the recorded state, and needs no state store or database. The analogue of Terraform's `show <planfile>`, and the read-only counterpart of `apply --plan-file`.
