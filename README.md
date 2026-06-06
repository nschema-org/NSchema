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
   nschema validate   # optional: check the schema files are well-formed first
   nschema plan
   nschema apply
   ```

`nschema init` is the easiest way to get a valid `nschema.json` — see [Configuration](#configuration) for everything it can hold.

## Commands

The `plan`, `apply`, `refresh`, and `import` commands accept the [common options](#common-options) below (`import` uses the provider options, not the state ones). Any option can instead be set in `nschema.json` or via an environment variable (see: [Configuration](#configuration)). `init` (which only writes files) and `validate` (which only reads your schema files) connect to no database or state store.

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

**Needs:** only a desired schema (`--schema-dir`).

- `--schema-dir <path>` _(required)_ — directory containing the desired-schema files. _(config `schema.dir`)_
- `--format <yaml|json>` — the format the desired schema is expressed in. Defaults to `yaml`. _(config `schema.format`)_
- `--schema-pattern <pattern>` — glob matched within the schema directory. Defaults to `**/*.yaml` or `**/*.json`. _(config `schema.pattern`)_

```sh
nschema validate --schema-dir ./schemas
```

### Common options

Available to all commands. These select the database and state store that hold the current schema.

- `--provider <postgres>` — the database supplying the live schema. Supported: `postgres`. With no provider, only offline operations (plan/refresh against a state store) are available. _(config `provider.postgres`, env `NSCHEMA_PROVIDER`)_
- `--connection-string <value>` — connection string for the provider. _(config `provider.postgres.connectionString`, env `NSCHEMA_CONNECTION_STRING`)_
- `--state-file <path>` — path for a `file` state store. _(config `state.file.path`, env `NSCHEMA_STATE_FILE`)_
- `--state-s3-bucket <bucket>` — bucket for an `s3` state store. _(config `state.s3.bucket`, env `NSCHEMA_STATE_S3_BUCKET`)_
- `--state-s3-key <key>` — object key for an `s3` state store. _(config `state.s3.key`, env `NSCHEMA_STATE_S3_KEY`)_
- `--config <path>` — path to the config file. Defaults to `./nschema.json` if present.

### `nschema plan`

Compute and show the migration plan, without changing anything.

**Needs:** a desired schema (`--schema-dir`) and a current-state source — either a live database (`--provider` plus a
connection string) or, for offline planning, a state store (`--state-file`).

- `--schema-dir <path>` _(required)_ — directory containing the desired-schema files. _(config `schema.dir`)_
- `--format <yaml|json>` — the format the desired schema is expressed in. Defaults to `yaml`. _(config `schema.format`)_
- `--schema-pattern <pattern>` — glob matched within the schema directory. Defaults to `**/*.yaml` or `**/*.json`. _(config `schema.pattern`)_
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
`--state-s3-bucket`/`--state-s3-key`). It captures the **whole** schema and so takes no desired-schema or
`--scope` options.

```sh
nschema refresh --provider postgres --state-file ./nschema.state.json
```

### `nschema import`

Read the live database schema and write it out as desired-schema source files. Use this to adopt an existing database
into NSchema: import it, then check the generated files into source control and manage further changes with `plan`/`apply`.

**Needs:** a live database (`--provider` plus a connection string) and an output path (`--output`).

- `--output <path>` _(required)_ — where to write the generated files. A file path for `--partition None`; a directory root for `Schema` or `Table`.
- `--partition <none|schema|table>` — how to split the imported schema across files: `None` (a single file, the default), `Schema` (one file per namespace), or `Table` (one file per table).
- `--format <yaml|json>` — format for the generated files. Defaults to `yaml`. (Unlike the other commands, this sets the format written **out**.)
- `--tables <name>` — limit the import to specific tables. May be repeated.
- `--scope <name>` — limit the import to specific database schemas (namespaces). May be repeated.

```sh
nschema import --provider postgres --output ./schemas --partition Table
```

## Configuration

Settings come from three sources, in increasing order of precedence:

1. The `nschema.json` config file (or the file passed to `--config`).
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
