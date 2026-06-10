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

   This writes `nschema.json` and `schemas/example.sql`. Edit the sample to describe your desired schema, written in
   NSchema DDL — a declarative, SQL-flavoured schema language:

   ```sql
   -- schemas/example.sql
   CREATE SCHEMA app;

   CREATE TABLE app.widgets (
     id   bigint NOT NULL,
     name text,
     CONSTRAINT widgets_pkey PRIMARY KEY (id)
   );
   ```

2. Point at your database (the connection string is a secret, so prefer the environment):

   ```sh
   export NSCHEMA_POSTGRES_CONNECTION_STRING="Host=localhost;Database=app;Username=postgres;Password=postgres"
   ```

3. Preview the migration, then apply it (`nschema.json` already has the provider, and the desired schema is just your `*.sql` files, so no flags are needed):

   ```sh
   nschema validate   # optional: check the schema files are well-formed first
   nschema plan
   nschema apply
   ```

`nschema init` is the easiest way to get a valid `nschema.json` — see [Configuration](#configuration) for everything it can hold.

## Commands

The `plan`, `apply`, `refresh`, `import`, `destroy`, `show`, `drift`, and `force-unlock` commands all talk to a database or state store. **Those two — the provider and the state store — are defined in `nschema.json`, not via CLI flags** (with the connection string supplied through the environment); see [Database and state](#database-and-state). The CLI options each command takes are the *workflow* options listed below. `init` (which only writes files) and `validate` (which only reads your schema files) connect to no database or state store.

### `nschema init`

Scaffold an `nschema.json` and a sample schema in the current directory, to get a new project going. It connects to nothing.

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

**Needs:** only a desired schema — your `*.sql` files.

The desired schema is every `*.sql` file found recursively under the project directory. Run inside the project, or point at it with `--directory`. There is nothing to configure: no format (it's always NSchema DDL) and no directory or glob.

```sh
nschema validate --directory ./my-project
```

### Database and state

The live database (`provider`) and the state store (`state`) describe *where* your schema lives, so — like a Terraform backend — they're defined in [`nschema.json`](#nschemajson) rather than passed as flags each run. There is no `--provider` or `--state-*` option. The one value that doesn't belong in a committed file is the database password, so the connection string has an environment override:

- **Connection string** — `provider.postgres.connectionString` in `nschema.json`, or the `NSCHEMA_POSTGRES_CONNECTION_STRING` environment variable (which takes precedence). The variable names the Postgres provider on its own — just as `state.s3.*` names the S3 store — so no separate provider selector is needed.

The desired schema needs no config at all — it's every `*.sql` file under the project directory. So a project's *where* (database and state) lives in `nschema.json`, the schema lives in your `*.sql` files, and the CLI flags are just the per-run workflow knobs.

Every command also accepts:

- `--directory <dir>` — the project directory to run in; `nschema.json` and the relative paths inside it resolve here (like `terraform -chdir`). Defaults to the current directory.
- `--config <path>` — config file path, relative to `--directory`. Defaults to `nschema.json`.
- `--no-color` — disable colored output. _(env `NO_COLOR`)_

### `nschema plan`

Compute and show the migration plan, without changing anything.

**Needs:** a desired schema (your `*.sql` files) and a current-state source — either a live database (configured
`provider.postgres`) or, for offline planning, a state store (configured `state`). See [Database and state](#database-and-state).

- `--scope <name>` — limit the migration to specific database schemas (namespaces). May be repeated. _(config `scope`)_
- `--destructive-actions <error|warn|allow>` — policy for destructive changes. Defaults to `error`. _(config `destructiveActionPolicy`, env `NSCHEMA_DESTRUCTIVE_ACTION_POLICY`)_
- `--destroy` — preview the SQL that [`destroy`](#nschema-destroy) would run to tear the managed schema down, instead of a forward plan (Terraform's `plan -destroy`).

```sh
nschema plan
```

With `--destroy` the command previews a teardown rather than a forward migration. It takes the same inputs as
[`destroy`](#nschema-destroy) — a live database (configured `provider.postgres`) the teardown SQL is rendered against,
and a managed-schema source (a configured state store, or a desired schema to fall back on) — but only **shows** the SQL;
it never connects to apply it, prompts, or writes state. `--scope` and `--destructive-actions` don't apply to a teardown
and are ignored.

```sh
nschema plan --destroy
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
down — a configured state store (`state.file` or `state.s3`), or, with no store, your `*.sql` files to fall back on.
When a state store is configured it is refreshed after the teardown. Accepts `--scope` to limit the teardown to specific
namespaces, and `--auto-approve` to skip the prompt.

```sh
nschema destroy
```

### `nschema show`

Print the schema recorded in the state store as human-readable text. The live database is never contacted — this is a
read of what NSchema last recorded, useful for inspecting state or diffing it against `import` output.

**Needs:** a state store (configured `state.file` or `state.s3`). Accepts `--scope` to limit the output to specific
namespaces.

```sh
nschema show
```

### `nschema drift`

Check whether the live database has drifted from the recorded state, reporting the difference as a diff (recorded →
live, so an out-of-band change appears as an add and a manual drop as a remove). This is a pure observation: no
transformers or policies run, so it never fails on a policy violation.

**Needs:** a live database (configured `provider.postgres`) and a state store to compare against (configured
`state.file` or `state.s3`). Accepts `--scope` to limit the check to specific namespaces.

```sh
nschema drift
```

### `nschema force-unlock`

Forcibly release a stale lock on the state store. NSchema locks the state during write operations (`apply`, `destroy`,
`refresh`); if one is interrupted, the lock can be left behind and block further runs. This removes whatever lock is
currently held — use it only once you're sure no operation is still running, Terraform's `force-unlock` style. Because
overriding a live lock can corrupt shared state, it prompts for confirmation first; pass `--force` to skip the prompt.

**Needs:** a state store (configured `state.file` or `state.s3`); the lock lives with it. The live database is never
contacted. Accepts `--force` to skip the confirmation prompt.

```sh
nschema force-unlock
```

## Configuration

Settings come from three sources, in increasing order of precedence:

1. The `nschema.json` config file — discovered in the `--directory` (default: the current directory), or the file passed to `--config`. Relative paths inside it (e.g. `state.file.path`) resolve against that directory.
2. `NSCHEMA_*` environment variables.
3. Command-line options.

### `nschema.json`

```json
{
  "provider": { "postgres": { "connectionString": "Host=localhost;Database=app;..." } },
  "state":    { "file": { "path": "./nschema.state.json" } },
  "scope": ["app"],
  "destructiveActionPolicy": "Error"
}
```

`nschema.json` configures only *where* your schema lives (the database and state store). The desired schema itself is
not configured — it's every `*.sql` file found recursively under the project directory.

### Connection string

The database connection string is a secret. Supply it through the environment:

```sh
export NSCHEMA_POSTGRES_CONNECTION_STRING="..."
```

It can also be set in `nschema.json` under `provider.postgres.connectionString` (handy for a local throwaway database), but _please_ don't commit secrets to source control. The environment variable takes precedence when both are present.

## Desired schema files

Schema files are written in **NSchema DDL** — a declarative, SQL-flavoured schema language. It borrows SQL's
`CREATE TABLE` shape so it reads instantly to anyone who works with databases, but it describes *desired state*: you
write the final shape of the schema, never `ALTER`/migration steps. Column types are canonical and dialect-agnostic
(`bigint`, `text`, `varchar(255)`, `decimal(18,2)`, …); anything the library doesn't recognise passes through as-is.

The desired schema is every `*.sql` file found recursively under the project directory — split it across as many files
as you like (e.g. one per schema or per table). A more complete example:

```sql
CREATE SCHEMA shop;

--- Line items for an order.
CREATE TABLE shop.order_items (
  order_id    int           NOT NULL,
  product_id  int           NOT NULL,
  quantity    int           NOT NULL DEFAULT 1,
  unit_price  numeric(12,2) NOT NULL,

  CONSTRAINT order_items_pkey PRIMARY KEY (order_id, product_id),
  CONSTRAINT fk_order   FOREIGN KEY (order_id)   REFERENCES shop.orders (id)   ON DELETE CASCADE,
  CONSTRAINT chk_qty    CHECK (quantity > 0),

  INDEX ix_product (product_id)
);

GRANT SELECT, INSERT ON shop.order_items TO app_rw;
```

A `---` doc-comment before a declaration becomes that object's database comment (`COMMENT ON …`); ordinary `--` comments
are notes for the reader and are not persisted.

### Deployment scripts

Some setup can't be expressed declaratively — creating an extension, a role, a custom grant, or backfilling data. For
these, write **raw SQL** files named `*.pre.sql` or `*.post.sql`:

- `*.pre.sql` files run (in filename order) **before** the migration;
- `*.post.sql` files run **after** it.

They can live anywhere under the project, alongside your schema files (the `.pre.sql`/`.post.sql` suffix is what
distinguishes them — they're excluded from the desired schema, not parsed as NSchema DDL). `plan` previews them and
`apply` runs them; a numeric prefix orders them (`001_extensions.pre.sql`, `010_backfill.post.sql`).

> Deployment scripts run on **every** `apply`, so they must be **idempotent** (e.g. `CREATE EXTENSION IF NOT EXISTS …`).
> They are not one-time, versioned migrations.

## License

See [LICENSE](LICENSE).
