# Roadmap

This document, and the `roadmap` directory, contain plans for potential features within the NSchema ecosystem.

I use this as a way to explore different plans and designs, so please don't take anything you read here as a guarantee. That said, if you spot a feature
here that you have thoughts on, let me know!

## Where things are written down

- `ROADMAP.md` acts as a high level view of upcoming features/changes, broken into sections.
- `roadmap/` contains detailed documents on those features/changes, their design and their reasoning. They should be considered living design documents until a feature actually ships.
- This roadmap covers the entire NSchema ecosystem, regardless of where the feature/change actually needs to be implemented.

## V5.0

- **There is no specific gate for v5.0.** It will release with clean code, no (known) bugs, and no breaking changes left over.
- As far as I'm aware, I'm the only user of NSchema, so backwards compatibility is less important than doing it right.

## Future plans

### Editor Support

— New file extension so editors can key a grammar without hijacking plain `.sql`.
- Also helps [linting](roadmap/nsql-linting.md) (editorconfig sections key on globs).
- Improve the authoring experience of nsql.
- Time-sensitive: custom constructs keep growing, and the opt-in extension gets costlier as projects accumulate `.sql` globs.
- Pick the extension itself early even if implementation waits — it leaks into editorconfig examples, docs, and the CI recipe.

### [NSQL linting](roadmap/nsql-linting.md)

Lint and formatting for NSQL, configured through literal `.editorconfig` support — the style half of what was "custom policies".
Ruled 2026-07-20: lint runs at the syntax layer, suppression is path-scoped sections, and linting gets no NSQL grammar of its own.

### [Custom policies](roadmap/custom-policies.md)

Project and plan policies: correctness checks (like "all tables must have a primary key") and guardrails. Also allows providers to add their own
policies to validate features/actions the engine might not support.

- Candidate shape for the open exception story: **address-scoped exceptions** — keyed on `ObjectAddress` with `Covers` containment,
  the same grammar as plan scoping. Config, not language surface; where the config lives is still open.

### Broader migration support?

The hard part of database management is data migrations rather than schema migrations. Migration scripts are a good feature, but are there any other
things we could support with?

Candidates, roughly in order of fit:

- **Lock-hazard awareness** — some DDL takes heavy locks or rewrites the table. A provider-supplied plan policy
  (the seam [custom policies](roadmap/custom-policies.md) already anticipates) that warns about them.
- **Expand/contract** — the real-world pattern is add column → backfill → dual-write window → drop. Even a documented recipe
  plus a diagnostic when a rename touches a populated column would help.
- **Batched/resumable backfills** — long data migrations want batching and resume-on-failure. The run-once ledger is the natural
  place to record partial progress, but that stretches its remit — needs its own design pass.

### Seed / reference data

Declarative rows for lookup tables, diffed and applied like schema. Fits the declarative model, and it's the
genre-standard feature currently missing. Probably the next idea that deserves a design doc.

### CI/CD Integration

Are there any more features we can add around integration/deployment support? Any ways we can help verify a migration will go as planned, or help prepare for it?
Maybe specific integration for Testcontainers? What about better locking? Could we make use of database locking to improve migrations? Especially if
they could be taken manually via the CLI? How about a way to test a plan?

- **Plan rehearsal** — restore the recorded state snapshot into a throwaway database (Testcontainers), apply the real plan,
  report what happened, statement timings included. The snapshot exists so plans don't surprise; rehearsal is the payoff.
  Subsumes the Testcontainers question — that's the mechanism, this is the feature.
- **Database-native locking** — an `IStateLock` backed by Postgres advisory locks, so locking works without a lock-capable
  state backend. Manual lock/unlock via the CLI pairs with it.
- Caution: "verify a plan is still valid before applying" is composition (drift check, then apply) — staleness fields on
  plan files are already rejected, don't re-add.

### Other, more vague, plans

- **Row-level security for Postgres.** Pure fidelity. No dependency on roles. Pilot kind for the per-kind handler decomposition.
- **Roles slice + grant fidelity** — model carries only schema and table grants today. Missing: routine `EXECUTE`, sequence `USAGE`, and
  `ALTER DEFAULT PRIVILEGES` (the big one — its absence forces the `RUN ALWAYS` workaround this roadmap calls a smell).
- **Partitioning** — pairs with change scripts; attach/detach is substantially data movement.
- **Template arguments** — `TEMPLATE x(arg)` / `APPLY TEMPLATE x('val')`. The `{schema}` brace syntax was chosen so this could generalize.
- **Script drift in the drift report** — surface "run-once body changed since it executed".
  - Caveat: that pair is recorded-vs-*project*, so the project becomes a third input to drift, which today needs only the store and live provider.
  - Possible resolution: move the feature, not the input — plan already loads project and recorded state, so surface it as a
    plan-time diagnostic and keep drift two-input.
- **`lastWrite` state metadata** — who/when/version. Full audit log and event-sourced state both rejected.
- **Docs** — canonical CI recipe (drift check + plan-on-PR via exit codes and `--json`); rollback-story page stating plainly that run-once and
  change scripts are one-way.
