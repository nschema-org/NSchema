# Schema state backend: remaining work

> Most of this plan shipped in NSchema core (state store, versioned serialization, current-source
> selection, capture, and the Plan/Apply/Refresh operations). What's left lives **outside this repo**:
> an S3 state store package and the Abodio consumer wiring.
>
> First consumer: Abodio (`/Users/tomwolfe/Development/Abodio/database`) — render a schema diff on pull
> requests without the PR build having live database access.

## Delivered in NSchema core

The library API the remaining work builds on:

- **State store** — `ISchemaStateStore` (`Read`/`Write`), optional. Register via `UseSchemaStateStore<T>()`,
  `UseSchemaStateStore(instance)`, or `UseFileStateStore(path)` (the built-in `FileSchemaStateStore`). State
  is serialized as versioned JSON (`SchemaStateSerializer`, internal; `SqlType` handled by a custom converter).
- **Current-schema source** — `UseStateBackedCurrentSchema()` (read current from the store, offline) or
  `UseCurrentSchemaAuto()` (store when planning, live database when applying). Defaults to the live provider.
- **Operations** — `MigrationOperation { Plan, Apply, Refresh }`; `Plan` is the default. Configure with
  `RunOperation(...)`, or call `NSchemaApplication.Plan()` / `Apply()` / `Refresh()`.
- **Capture** — a successful `Apply` captures the resulting live schema to the store (when one is
  configured); `Refresh` captures without planning/applying (errors with no store). Capture re-reads the
  **live** provider and is scoped by `MigrationOptions.SchemaNames`.

Shipped in 2.0.0 as a breaking change: `DryRun` / `DryRunOnly` were removed outright (no obsolete shim),
and the default operation is now `Plan` rather than `Apply`.

## Remaining 1 — S3 state store (`NSchema.Aws`)

- New repo/package mirroring `NSchema.Postgres`.
- `S3SchemaStateStore : ISchemaStateStore` over the AWS SDK; configurable bucket + key, plus a builder
  extension to register it (e.g. `UseS3StateStore(bucket, key)`).
- **Locking** is out of scope for v1 — document last-write-wins. Revisit with S3 conditional writes /
  DynamoDB if concurrent applies become a problem (Abodio deploys can interleave).
- Open: `NSchema.Aws` as a new repo vs a folder; package id / namespace.

## Remaining 2 — Abodio consumer wiring

- Reference `NSchema.Aws`; register `S3SchemaStateStore` (bucket/key per environment).
- **Deploy** (in-VPC ECS task): `UsePostgres(...)` + S3 store + `Apply` → applies and captures state to S3.
- **PR build** (read-only role, `s3:GetObject` only): `UseStateBackedCurrentSchema()` (reads S3) + `Plan`
  → render diff → post via `ICommentService`.
- Add a `DatabasePlan` / `DatabaseDeploy` step pair (mirroring `TofuPlan` / `TofuApply`); retire the
  `EcsRun` step and the `EcsRunOptions.Deploy` flag.
- Optional scheduled in-VPC `Refresh` task to narrow the drift window between deploys.
- IAM: read-only role gets `s3:GetObject`; deploy/refresh role gets `s3:PutObject`.
