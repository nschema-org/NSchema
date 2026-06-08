# ![NSchema](https://raw.githubusercontent.com/nschema-org/NSchema.Core/main/assets/nschema-logo-horizontal.png)

[![NSchema](https://github.com/nschema-org/NSchema/actions/workflows/cicd.yml/badge.svg)](https://github.com/nschema-org/NSchema/actions/workflows/cicd.yml)

> [!WARNING]
> This project is still very new, and although I'm making great headway, there's no stable release just yet.
> Feel free to follow my progress though, or ask questions if you're interested. Just know it's not production ready just yet.

# NSchema

A declarative database schema migration tool. You describe the schema you want, and NSchema computes and applies the migration to get there.

## Installation

```sh
dotnet tool install --global nschema
```

This installs the `nschema` command.

## Quickstart

1. Scaffold a config and a sample schema:

   ```sh
   nschema init
   ```

   This writes `nschema.json` and `schemas/example.yaml`. Edit the sample to describe your desired schema:

   ```yaml
   # schemas/example.yaml
   schemas:
     - name: app
       tables:
         - name: widgets
           primaryKey:
             name: widgets_pkey
             columnNames: [id]
           columns:
             - name: id
               type: bigint
               isNullable: false
             - name: name
               type: text
               isNullable: true
   ```

2. Point at your database (the connection string is a secret, so prefer the environment):

   ```sh
   export NSCHEMA_POSTGRES_CONNECTION_STRING="Host=localhost;Database=app;Username=postgres;Password=postgres"
   ```

3. Preview the migration, then apply it (`nschema.json` already has the provider and schema directory, so no flags are needed):

   ```sh
   nschema validate   # optional: check the schema files are well-formed first
   nschema plan
   nschema apply
   ```

`nschema init` is the easiest way to get a valid `nschema.json` — see [Configuration](#configuration) for everything it can hold.

## Commands

The `plan`, `apply`, `refresh`, `import`, and `destroy` commands all talk to a database, and most also use a state store. **Those two — the provider and the state store — are defined in `nschema.json`, not via CLI flags** (with the connection string supplied through the environment); see [Database and state](#database-and-state). The CLI options each command takes are the *workflow* options listed below. `init` (which only writes files) and `validate` (which only reads your schema files) connect to no database or state store.

### `nschema init`

Scaffold an `nschema.json` and a sample schema in the current directory, to get a new project going. It connects to nothing.

- `--format <yaml|json>` — format for the generated config and sample schema. Defaults to `yaml`.
- `--force` — overwrite an existing `nschema.json`.

```sh
nschema init
```

### `nschema validate`

Check that your desired-schema files are well-formed and internally consistent, without contacting a database or state store. Useful as a fast pre-flight check in CI. It exits non-zero if it finds an error and zero otherwise; warnings are reported but do not fail the command.

It verifies that:

- every file parses;
- primary keys, indexes, and foreign keys reference columns that exist, and foreign keys reference a table whose primary key or a unique index matches the referenced columns (**errors**);
- tables have a primary key, primary-key columns aren't nullable, and no key or index lists a column twice (**warnings**).

**Needs:** only a desired schema, configured in `nschema.json`.

The schema's location and shape describe how the project's files are laid out, so they all live in config rather than as per-run flags: **dir** (`schema.dir`), **format** (`schema.format`, default `yaml`), and **glob** (`schema.pattern`, default `**/*.yaml` or `**/*.json`). Run inside the project, or point at it with `--directory`.

```sh
nschema validate --directory ./my-project
```

### Database and state

The live database (`provider`) and the state store (`state`) describe *where* your schema lives, so — like a Terraform backend — they're defined in [`nschema.json`](#nschemajson) rather than passed as flags each run. There is no `--provider` or `--state-*` option. The one value that doesn't belong in a committed file is the database password, so the connection string has an environment override:

- **Connection string** — `provider.postgres.connectionString` in `nschema.json`, or the `NSCHEMA_POSTGRES_CONNECTION_STRING` environment variable (which takes precedence). The variable names the Postgres provider on its own — just as `state.s3.*` names the S3 store — so no separate provider selector is needed.

The schema source is config too — `schema.dir` (plus `schema.format`/`schema.pattern`) — so a project's *where* (database, state, schema files) lives entirely in `nschema.json`, and the CLI flags are just the per-run workflow knobs.

Every command also accepts:

- `--directory <dir>` — the project directory to run in; `nschema.json` and the relative paths inside it resolve here (like `terraform -chdir`). Defaults to the current directory.
- `--config <path>` — config file path, relative to `--directory`. Defaults to `nschema.json`.
- `--no-color` — disable colored output. _(env `NO_COLOR`)_

### `nschema plan`

Compute and show the migration plan, without changing anything.

**Needs:** a desired schema (configured `schema.dir`) and a current-state source — either a live database (configured
`provider.postgres`) or, for offline planning, a state store (configured `state`). See [Database and state](#database-and-state).

- `--scope <name>` — limit the migration to specific database schemas (namespaces). May be repeated. _(config `scope`)_
- `--destructive-actions <error|warn|allow>` — policy for destructive changes. Defaults to `error`. _(config `destructiveActionPolicy`, env `NSCHEMA_DESTRUCTIVE_ACTION_POLICY`)_

The schema **format** (`schema.format`) and **glob** (`schema.pattern`) are config-only — see [`validate`](#nschema-validate).

```sh
nschema plan
```

### `nschema apply`

Compute the plan and apply it to the target database. Prompts for confirmation before making changes unless
`--auto-approve` is given.

**Needs:** the same inputs as `plan`, against a live database the tool can write to.

Accepts every [`plan`](#nschema-plan) option, plus:

- `--auto-approve` — skip the confirmation prompt and apply immediately.

```sh
nschema apply
```

### `nschema refresh`

Read the live schema and write it to the state store. Use this to seed or repair state.

**Needs:** a live database (configured `provider.postgres`) and a state store to write to (configured `state.file` or
`state.s3`). It captures the **whole** schema and so takes no desired-schema or `--scope` options — with both inputs in
`nschema.json`, it runs with no flags at all.

```sh
nschema refresh
```

### `nschema import`

Read the live database schema and write it out as desired-schema source files. Use this to adopt an existing database
into NSchema: import it, then check the generated files into source control and manage further changes with `plan`/`apply`.

**Needs:** a live database (configured `provider.postgres`) and an output path (`--output`).

- `--output <path>` _(required)_ — where to write the generated files. A file path for `--partition None`; a directory root for `Schema` or `Table`.
- `--partition <none|schema|table>` — how to split the imported schema across files: `None` (a single file, the default), `Schema` (one file per namespace), or `Table` (one file per table).
- `--format <yaml|json>` — format for the generated files. Defaults to `yaml`. (This sets the format written **out**, distinct from the `schema.format` other commands read.)
- `--tables <name>` — limit the import to specific tables. May be repeated.
- `--scope <name>` — limit the import to specific database schemas (namespaces). May be repeated.

```sh
nschema import --output ./schemas --partition Table
```

### `nschema destroy`

Drop all managed schema objects from the target database. Prompts for confirmation before making changes unless
`--auto-approve` is given. This is purely destructive and is exempt from the destructive-action policy — it's the
intended escape hatch for tearing a managed schema back down.

**Needs:** a live database (configured `provider.postgres`) the tool can write to, and a source for the schema to tear
down — a configured state store (`state.file` or `state.s3`), or, with no store, a desired schema (configured
`schema.dir`) to fall back on. When a state store is configured it is refreshed after the teardown. Accepts `--scope` to
limit the teardown to specific namespaces, and `--auto-approve` to skip the prompt.

```sh
nschema destroy
```

## Configuration

Settings come from three sources, in increasing order of precedence:

1. The `nschema.json` config file — discovered in the `--directory` (default: the current directory), or the file passed to `--config`. Relative paths inside it (`schema.dir`, `state.file.path`) resolve against that directory.
2. `NSCHEMA_*` environment variables.
3. Command-line options.

### `nschema.json`

```json
{
  "provider": { "postgres": { "connectionString": "Host=localhost;Database=app;..." } },
  "state":    { "file": { "path": "./nschema.state.json" } },
  "schema":   { "dir": "./schemas", "format": "yaml", "pattern": "**/*.yaml" },
  "scope": ["app"],
  "destructiveActionPolicy": "Error"
}
```

### Connection string

The database connection string is a secret. Supply it through the environment:

```sh
export NSCHEMA_POSTGRES_CONNECTION_STRING="..."
```

It can also be set in `nschema.json` under `provider.postgres.connectionString` (handy for a local throwaway database), but _please_ don't commit secrets to source control. The environment variable takes precedence when both are present.

## Desired schema files

A schema file is a document with a `schemas` array; each schema has `tables`, and each table has `columns` (and an
optional `primaryKey`). Column `type` is a compact string such as `bigint`, `text`, `varchar(255)`, or `decimal(18,2)`.
The YAML and JSON formats describe the same structure — the YAML quickstart above is equivalent to:

```json
{
  "schemas": [
    {
      "name": "app",
      "tables": [
        {
          "name": "widgets",
          "primaryKey": { "name": "widgets_pkey", "columnNames": ["id"] },
          "columns": [
            { "name": "id", "type": "bigint", "isNullable": false },
            { "name": "name", "type": "text", "isNullable": true }
          ]
        }
      ]
    }
  ]
}
```

## License

See [LICENSE](LICENSE).
