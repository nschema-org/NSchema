# NSchema.Cli

A declarative database schema migration tool. You describe the schema you want, and NSchema computes and applies the migration to get there.

## Installation

```sh
dotnet tool install --global NSchema.Cli
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
   export NSCHEMA_CONNECTION_STRING="Host=localhost;Database=app;Username=postgres;Password=postgres"
   ```

3. Preview the migration, then apply it (`nschema.json` already has the provider and schema directory, so no flags are needed):

   ```sh
   nschema plan
   nschema apply
   ```

`nschema init` is the easiest way to get a valid `nschema.json` — see [Configuration](#configuration) for everything it can hold.

## Commands

The `plan`, `apply`, and `refresh` commands accept the [common options](#common-options) below. Any option can instead be set in `nschema.json` or via an environment variable (see: [Configuration](#configuration)). `init` is standalone — it only writes files.

### `nschema init`

Scaffold an `nschema.json` and a sample schema in the current directory, to get a new project going. It connects to nothing.

- `--format <yaml|json>` — format for the generated config and sample schema. Defaults to `yaml`.
- `--force` — overwrite an existing `nschema.json`.

```sh
nschema init
```

### Common options

Available to all commands. These select the database and state store that hold the current schema.

- `--provider <postgres>` — the database supplying the live schema. Supported: `postgres`. With no provider, only offline operations (plan/refresh against a state store) are available. _(config `provider.type`, env `NSCHEMA_PROVIDER`)_
- `--connection-string <value>` — connection string for the provider. _(config `provider.connectionString`, env `NSCHEMA_CONNECTION_STRING`)_
- `--state-type <file|s3>` — where state snapshots are kept: `file` (default; connection string is a path) or `s3`
  (connection string is an `s3://bucket/key` URI). _(config `state.type`, env `NSCHEMA_STATE_TYPE`)_
- `--state-connection-string <value>` — connection string for the state store. _(config `state.connectionString`, env `NSCHEMA_STATE_CONNECTION_STRING`)_
- `--state-file <path>` — shorthand for a `file` state store at `<path>`. _(config `state.connectionString`)_
- `--config <path>` — path to the config file. Defaults to `./nschema.json` if present.

### `nschema plan`

Compute and show the migration plan, without changing anything.

**Needs:** a desired schema (`--schema-dir`) and a current-state source — either a live database (`--provider` plus a
connection string) or, for offline planning, a state store (`--state-file`).

- `--schema-dir <path>` _(required)_ — directory containing the desired-schema files. _(config `schema.dir`)_
- `--format <yaml|json>` — the format the desired schema is expressed in. Defaults to `yaml`. _(config `schema.format`)_
- `--schema-glob <glob>` — glob matched within the schema directory. Defaults to `**/*.yaml` or `**/*.json`. _(config `schema.glob`)_
- `--scope <name>` — limit the migration to specific database schemas (namespaces). May be repeated. _(config `scope`)_
- `--destructive-actions <error|warn|allow>` — policy for destructive changes. Defaults to `error`. _(config `destructiveActionPolicy`, env `NSCHEMA_DESTRUCTIVE_ACTION_POLICY`)_

```sh
nschema plan --provider postgres --schema-dir ./schemas
```

### `nschema apply`

Compute the plan and apply it to the target database. Prompts for confirmation before making changes unless
`--auto-approve` is given.

**Needs:** the same inputs as `plan`, against a live database the tool can write to.

Accepts every [`plan`](#nschema-plan) option, plus:

- `--auto-approve` — skip the confirmation prompt and apply immediately.

```sh
nschema apply --provider postgres --schema-dir ./schemas
```

### `nschema refresh`

Read the live schema and write it to the state store. Use this to seed or repair state.

**Needs:** a live database (`--provider` plus a connection string) and a state store to write to (`--state-file`, or
`--state-type`/`--state-connection-string`). It captures the **whole** schema and so takes no desired-schema or
`--scope` options.

```sh
nschema refresh --provider postgres --state-file ./nschema.state.json
```

## Configuration

Settings come from three sources, in increasing order of precedence:

1. The `nschema.json` config file (or the file passed to `--config`).
2. `NSCHEMA_*` environment variables.
3. Command-line options.

### `nschema.json`

```json
{
  "provider": { "type": "postgres", "connectionString": "Host=localhost;Database=app;..." },
  "state":    { "type": "file", "connectionString": "./nschema.state.json" },
  "schema":   { "dir": "./schemas", "format": "yaml", "glob": "**/*.yaml" },
  "scope": ["app"],
  "destructiveActionPolicy": "Error"
}
```

### Connection string

The database connection string is a secret. Prefer supplying it through the environment:

```sh
export NSCHEMA_CONNECTION_STRING="..."
```

It can also be passed with `--connection-string` or set in `nschema.json`, but _please_ don't commit secrets to source control.

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
