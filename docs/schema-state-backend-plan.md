# Implementation plan: schema state backends + plan/apply/refresh operations

> Status: design agreed, not yet implemented. Author handoff doc.
> First consumer: Abodio (`/Users/tomwolfe/Development/Abodio/database`), which wants to render a
> schema diff on pull requests **without** the PR build having live database access.

## Motivation

A migration plan is `diff(desired_schema_from_code, current_schema)`. Today "current schema" can only
come from a live database connection (`PostgresSchemaProvider`). That forces anyone who wants an
offline/preview plan (e.g. a PR build with no VPC access and a read-only role) to reach the live DB.

This is the same problem Terraform solves with **state**: cache a serialized snapshot of the current
world after each apply, and let `plan` diff against that cache instead of the live target. NSchema is
already shaped for this — `ISchemaProvider` is a single abstraction used for both desired- and
current-state, swappable via `UseCurrentSchema<T>()`, and `DatabaseSchema` is a plain serializable model.

This plan adds:
1. A pluggable **state store** (`ISchemaStateStore`) + versioned JSON serialization of `DatabaseSchema`.
2. A **state-backed current-schema provider** for offline plans.
3. **State capture** after apply / a standalone **refresh**.
4. An explicit **operation** model (`Plan`/`Apply`/`Refresh`) replacing the `DryRun` boolean, with a
   semver-safe deprecation shim.
5. Explicit `migration.Plan()/Apply()/Refresh()` entry points alongside config-bound `RunAsync()`.

## Terraform mental model → NSchema mapping

| Terraform | NSchema |
| --- | --- |
| config (HCL) | desired schema providers (`AddSchemasFromAssembly…`) |
| live infra read | `PostgresSchemaProvider` (live current-state / introspection source) |
| state file + backend | `DatabaseSchema` JSON + `ISchemaStateStore` |
| `plan` (`-refresh=false`) | `Plan` op with state-backed current provider (offline) |
| `plan` (refresh) | `Plan` op with live current provider (in-VPC) |
| `apply` | `Apply` op: plan → execute → write state |
| `refresh` | `Refresh` op: read live → write state, no execute |

Key difference from Terraform: desired state is **C# code**, not data files. Any future CLI must load
the consumer's compiled schema assembly (a `dotnet ef`-style design-time factory), so the CLI is
deferred — it should be a thin automation wrapper over the library API designed here.

## 1. State store abstraction (NSchema core)

`src/NSchema/State/ISchemaStateStore.cs`:

```csharp
public interface ISchemaStateStore
{
    /// Returns the persisted schema, or null if no state exists yet (bootstrap).
    Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default);
    Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default);
}
```

- Ship `LocalFileSchemaStateStore` in core (path-based).
- External backends ship as separate packages (see §3), mirroring `NSchema.Postgres`.
- Builder sugar, e.g. `UseSchemaStateStore<T>()`, registering the store as a singleton.
- The store is **optional**: with no store registered, state writes are no-ops and `Refresh` errors
  with a clear message.

### Serialization & versioning

- `DatabaseSchema` is a plain model we own → guarantee JSON round-trip with `System.Text.Json`
  (add `[JsonConstructor]` / map the `Create`-with-options shape; add a converter only if needed).
- Wrap the payload in a **versioned envelope**: `{ stateFormatVersion, nschemaVersion, schemaNames, schema }`.
  - `stateFormatVersion` gates forward/backward compat and a future upgrade path.
  - `schemaNames` records the managed scope the snapshot covers (see scoping note in §6).
- Add a round-trip test that serializes a rich `DatabaseSchema` and asserts structural equality.

## 2. State-backed current-schema provider (NSchema core)

`StateBackedSchemaProvider : ISchemaProvider` → returns `store.Read()` (empty `DatabaseSchema` when null,
so a first-run plan shows a full create). Registered into the keyed current-state slot
(`ISchemaProvider.CurrentSchemaProviderKey`) via the existing `UseCurrentSchema<T>()`.

- Online config (in-VPC): current provider = `PostgresSchemaProvider` (live).
- Offline config (CI/PR): current provider = `StateBackedSchemaProvider`.

This is the only switch that decides online vs offline planning; it is registration-time config, not a
runtime mode.

## 3. S3 state store (new package `NSchema.Aws`)

- New repo/package mirroring `NSchema.Postgres`.
- `S3SchemaStateStore : ISchemaStateStore` over the AWS SDK; configurable bucket + key.
- Builder/DI extension to register it.
- **Locking** (Terraform-style) is out of scope for v1 — document last-write-wins. Revisit with S3
  conditional writes / DynamoDB if concurrent applies become a problem (Abodio deploys can interleave).

## 4. Operation model + DryRun deprecation (semver-safe)

`src/NSchema/Migration/MigrationOperation.cs`:

```csharp
public enum MigrationOperation { Plan, Apply, Refresh }
```

`MigrationOptions`:

```csharp
public MigrationOperation Operation { get; set; } = MigrationOperation.Apply; // == old DryRun=false default

[Obsolete("Use Operation instead. DryRun will be removed in 2.0.0.")]
public bool DryRun
{
    get => Operation == MigrationOperation.Plan;
    set => Operation = value ? MigrationOperation.Plan : MigrationOperation.Apply;
}
```

- `Operation` is the single source of truth; `DryRun` is a pure projection (no dual state).
- Default `Apply` preserves today's behaviour (`DryRun` defaulted to `false`) → non-breaking.
- Reimplement `NSchemaApplicationBuilder.DryRunOnly` to set `Operation` directly and mark it
  `[Obsolete]` (avoids the library generating its own obsolete-usage warnings).
- Migrate the 3 `DefaultMigrationPipeline` reads of `DryRun` to `Operation`.
- **2.0.0 cleanup:** delete `DryRun`, `DryRunOnly`, and the projection.

## 5. Pipeline branches (NSchema core)

Change the internal contract to take the operation as a parameter (internal → non-breaking):

```csharp
internal interface IMigrationPipeline { Task Run(MigrationOperation operation, CancellationToken ct = default); }
```

`DefaultMigrationPipeline.Run(operation, ct)`:

- **Plan**: `planner.Plan` → render → `executor.Apply(plan, dryRun:true)` (shows SQL, no execution).
- **Apply**: `planner.Plan` → render → `executor.Apply(plan, dryRun:false)` → **write state** (see §6).
- **Refresh**: skip planner/executor entirely → **write state** (see §6).

## 6. State capture rules

- State is written by **re-reading the keyed current provider** after the operation
  (`PostgresSchemaProvider` re-queries on each call — verified no caching), so a post-apply read
  returns the **actual** resulting schema (drift + applied changes), not the desired plan target.
  Do **not** capture by serializing the desired plan — that loses drift fidelity.
- Uniform rule for both `Apply` and `Refresh`: write = `currentProvider.GetSchema(SchemaNames)` →
  `store.Write(...)`. No separate "introspection source" role needed.
- Consequence/constraint: `Apply` and `Refresh` require a **live** current provider (normal in-VPC
  config). Guard: if the current provider is the state-backed one, throw
  *"refresh/apply requires a live schema source"* rather than silently round-tripping state.
- Scope the capture read by `MigrationOptions.SchemaNames` so state records exactly the managed scope.
- An `Apply` with an empty diff still writes state → keeps state fresh; makes "apply ⇒ implicit refresh" hold.
- `Apply` therefore doubles as a refresh; standalone `Refresh` only catches drift **between** applies.

## 7. Entry points: config-bound + explicit, one lifecycle

Both paths must go through the **real host** (startup validation, hosted services, graceful shutdown,
disposal). Do **not** resolve+run the pipeline outside the host.

- Internal run context (mutable singleton, safe because a host runs once):

  ```csharp
  internal sealed class MigrationRunContext { public MigrationOperation? Override { get; set; } }
  ```

- `NSchemaHost.ExecuteAsync`: `var op = ctx.Override ?? options.Value.Operation; await pipeline.Run(op, ct);`
- Public methods on `NSchemaApplication`:

  ```csharp
  public Task Plan(CancellationToken ct = default)    => RunOperation(MigrationOperation.Plan, ct);
  public Task Apply(CancellationToken ct = default)   => RunOperation(MigrationOperation.Apply, ct);
  public Task Refresh(CancellationToken ct = default) => RunOperation(MigrationOperation.Refresh, ct);

  private Task RunOperation(MigrationOperation op, CancellationToken ct)
  {
      _host.Services.GetRequiredService<MigrationRunContext>().Override = op;
      return ((IHost)this).RunAsync(ct); // full lifecycle
  }
  ```

- **Precedence:** explicit override > configured `Operation`; the override is consulted only by
  `NSchemaHost`.
- **Single-run:** explicit methods go through `StartAsync`, tripping the existing `_hasRun` guard —
  calling two throws "can only be started once" (correct host semantics).
- `RunAsync()` (no override) keeps working exactly as today → Abodio's `await migration.RunAsync()`
  needs no change.

## 8. Abodio consumer wiring (after NSchema ships)

- Reference `NSchema.Aws`; register `S3SchemaStateStore` (bucket/key per environment).
- **Deploy** (in-VPC ECS task): live Postgres current provider + S3 store; run `Apply` → writes state to S3.
- **PR build** (read-only role, `s3:GetObject` only): `StateBackedSchemaProvider` (reads S3) + run
  `Plan` → render diff → post via `ICommentService`.
- Add a `DatabasePlan` / `DatabaseDeploy` step pair (mirroring `TofuPlan`/`TofuApply`); retire the
  `EcsRun` step and the `EcsRunOptions.Deploy` flag.
- Optional scheduled in-VPC `Refresh` task to narrow the drift window between deploys.
- IAM: read-only role gets `s3:GetObject`; deploy/refresh role gets `s3:PutObject`.

## 9. Testing

- `DatabaseSchema` JSON round-trip (structural equality on a rich schema).
- `StateBackedSchemaProvider`: null state → empty schema; populated state → exact.
- Pipeline: Plan (no write, no execute), Apply (writes read-back state, incl. empty-diff), Refresh
  (writes, no execute), Refresh/Apply against a state-backed current provider → throws.
- `DryRun` ⇄ `Operation` projection + default behaviour unchanged.
- Entry points: explicit override beats config; second run throws; `RunAsync()` uses config operation.
- `LocalFileSchemaStateStore` read/write/round-trip; missing file → null.

## 10. Semver checklist (1.x)

Additive only: new enum, `Operation` property, `Plan/Apply/Refresh` methods, `ISchemaStateStore` +
providers + builder sugar. `DryRun`/`DryRunOnly` obsolete but functional. `IMigrationPipeline`
signature change is internal. No behavioural change to existing default. → minor version bump.

## Open decisions (resolve during implementation)

- Exact `DatabaseSchema` JSON shape — does the `Create`-with-options pattern need a custom converter?
- `NSchema.Aws` as a new repo vs a folder; package id/namespace.
- Whether `LocalFileSchemaStateStore` belongs in core or its own package (leaning core).
- Builder method names: `UseSchemaStateStore<T>()`, etc.