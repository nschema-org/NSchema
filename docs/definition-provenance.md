# Definition provenance

Status: accepted 2026-08-05; in progress.

The plan for making hand-authored definitions converge: the state records both spellings of a
body-bearing object, and every comparison stays within one language.

## The problem

- A body-bearing object (view, materialized view, function, procedure, aggregate, trigger) carries
  verbatim SQL the engine may not store verbatim.
- SQL Server keeps the author's text (`sys.sql_modules`), so declared-vs-introspected compares like
  with like and converges today.
- Postgres stores the parsed tree and *deparses* on introspection: it schema-qualifies (by the
  `search_path` at deparse time), expands `SELECT *`, inserts `::type` casts, parenthesizes join
  conditions, reflows the function wrapper, and renames dollar-quote tags.
- So on Postgres, a hand-authored definition never textually equals its introspected form. After a
  clean apply, every plan still wants a replace — a permanent diff on every hand-authored routine.

Found by the gauntlet (2026-08-04): the first hand-authored scenarios
(`NSchema.Gauntlet/scenarios-pending/`) applied cleanly and failed convergence on Postgres only.
The corpus never sees this: an imported project carries the deparsed spelling on both sides.

## Rejected

- **Semantic canonicalization** — parsing both sides and normalizing qualification, casts, star
  expansion. Reimplements the engine's analyzer; unbounded fidelity chase; false equivalences are
  worse than false diffs. Already rejected once for view bodies; the ruling extends.
- **Stronger cosmetic normalization** — whitespace, case, dollar-tags. Cannot un-expand a star or
  un-insert a cast. Fixes the demo, not the problem.
- **Author discipline** ("write bodies the way the engine re-renders them") — the rendering depends
  on `search_path` and catalog state; nobody can follow it.

## The design

The state records the *pair* for every managed body-bearing object:

- **declared** — the text the project applied.
- **captured** — the engine's own rendering, introspected after that apply.

Every comparison then stays within one language:

- **Plan** (project vs recorded state) compares declared vs declared.
  A hand-authored routine converges the moment it is applied. No normalization involved.
- **Drift** (live vs recorded state) compares captured vs captured.
  Both sides are deparsed by the same engine, so exact equality reliably detects out-of-band
  edits — which cosmetic normalization can currently mask.
- An edit beyond `SqlText`'s cosmetic normalization plans a replace. That is honest: NSchema cannot
  know two spellings are equivalent without the engine's analyzer, and the replace is harmless.
  Whitespace-only edits are still absorbed — within one language the cosmetic normalizer is sound.

### The fallback

The declared half is not rebuildable from the database. When it is absent, comparison falls back
to the captured half — today's behavior, possibly wrong in the same way it is wrong today — and
the next apply records the declared half, so the pair is **self-healing**:

- legacy state, before this feature: fallback until the next apply.
- an object adopted or imported: declared and captured coincide (import writes the engine's
  spelling), so the fallback is exact.
- state captured while the object was unmanaged: fallback until the next apply.

This makes the declared half the second deliberate exception to "state is a rebuildable cache",
after the run-once ledger — and it earns it the same way: it is something only the apply ever knew.

### Refresh

- Refresh recaptures the captured half, as today.
- The declared half survives a refresh **while the recaptured text equals the captured text it was
  recorded with**. The engine re-rendering identically means the object has not changed.
- If they differ, the object drifted out of band: the declared half is dropped as stale, the
  fallback applies, and the next apply re-establishes the pair.

### Apply

- The plan carries the declared set, exactly as it carries `Managed`: computed at plan time —
  within the plan's scope, the definitions the project declares (implicit objects excluded);
  outside it, whatever is already recorded. `DeclaredAfterApply` mirrors `ManagedAfterApply`.
- Riding the plan makes a saved plan file self-consistent: applying a stale file records the
  spellings that were actually planned and executed, not whatever the project says at apply time.
- After executing a plan, the apply already refreshes the state store. At that point the plan's
  declared set is recorded wholesale. The database was just made to match the plan, so recording
  all of it is correct — not only the objects this plan touched.
- A failed apply refreshes without the plan, so no declared set is recorded; the recapture drops
  the declared halves of whatever the partial run changed.

### What shrinks

- The pressure for provider-side body equivalence disappears. `SqlText.EquivalentTo`'s cosmetic
  normalization stays, but plan comparison now runs it within one language, where it is sound —
  no `SqlEquivalence` body seam is needed at all. On engines that preserve source (SQL Server),
  declared and captured coincide and nothing changes.
- The same mechanism later covers CHECK expressions and column defaults, which Postgres deparses
  the same way — the same latent bug, not yet surfaced.

## Consequences

- State payload: one additive `declared` field beside the snapshot. No state-version bump (no consumers).
- Plan files: one additive `declared` field on the persisted plan. No format-version bump.
- Providers: no changes required — the engine half is what introspectors already capture.
- Gauntlet: un-park `scenarios-pending/` (routine-body-change, routine-signature-change,
  materialized-view-body-change, trigger-body-change); all four should go green on Postgres.

## Resolved (2026-08-05)

- **Where the declared half lives** — beside the snapshot, not on the model. A `DefinitionSet`
  (`Model/`, beside `IdentitySet`: identities say which objects, definitions say how their bodies
  are spelled) holds per-kind entries keyed by address — views, routines, triggers.
  `DatabaseState.Declared` records it, the third piece of apply-only knowledge after the ledger
  and the managed set, and `MigrationPlan.Declared` carries it the way `Managed` is carried.
  The shared `Database` model stays role-free, and the payload cannot leak into the diff surface:
  the comparer never sees the set.
- **Where the languages are chosen** — `ProjectComparer` overlays the declared set onto the
  current side before aligning (`Database.WithDefinitions`), so the plan path compares
  declared-vs-declared with the captured text as the per-object fallback. Drift calls
  `IDatabaseComparer` directly with the raw snapshot, so it is captured-vs-captured with no code
  at all — which also answers the drift-wording question: drift never consults the declared half.
- **No force-drop flag on refresh** — the carry-over rule already drops a stale declared half on
  its own; there is nothing left for a flag to force. Not added until a real need shows up.
