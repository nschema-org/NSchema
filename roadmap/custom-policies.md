# Custom policies

Project and plan policies: correctness checks and guardrails, and the plugin seam for user-written rules.
**The lint half split out to [[nsql-linting]] on 2026-07-20** — this doc keeps what runs against the model
and the plan.

## The split (2026-07-20)

"Custom policies" was never one feature. **What a rule is about determines the layer it runs at, and the
layer determines its config surface:**

| Layer               | About              | Examples                              | Config surface        |
|---------------------|--------------------|---------------------------------------|-----------------------|
| Lint (syntax)       | the text           | naming, preferred syntax, directives  | `.editorconfig`       |
| Project policy      | the declared state | no-PK, nullable-PK, duplicate columns | TBD (see exceptions)  |
| Plan policy         | the change         | destructive, data hazard, enum removal| flag/env (unchanged)  |

- Lint is designed and moving ([[nsql-linting]]). Project/plan come later — **that's where plugin support
  lands**, because those checks run inside the plan/apply pipeline where the ALC rails already operate.
  Lint has no plugin story; its rules register compile-time via the builder.
- One package shipping many rule sets remains a plausible use of the qualified plugin form (the
  plugin-maturation work has since shipped).

## The motivating finding, corrected

- The old finding stands mechanically: `SchemaLintPolicy` emits four distinct rules under one `Source`,
  so `Source` is policy identity, not rule identity.
- But the classification was wrong: **all four checks are correctness, not lint.** No-PK is only
  answerable at the model — a PK can be declared in a different file than its table. Nullable-PK and
  duplicate-column checks are contradictions. The class is misnamed, not misplaced: it stays an
  `IProjectPolicy` and wants a rename.
- Consequence: the policy that motivated per-rule editorconfig severity contains zero rules that
  editorconfig governs. The first real lint rules are the naming conventions, which don't exist yet.

## What survives from the old spine

- **Rule identity (RULED 2026-07-15, survives):** `PolicyDiagnostic : Diagnostic` carrying the rule id and
  the offending address. Not on base `Diagnostic` — not every diagnostic is a rule.
- **Rule id spelling (ruled 2026-07-20 in [[nsql-linting]]):** dot-separated slugs, underscore words —
  shared across lint and policy so ids read the same everywhere.
- **Severity vocabulary (ruled 2026-07-20):** adopt `error`/`warning`/`suggestion`/`silent`/`none`;
  `PolicyEnforcement` maps onto it.
- **Central map over self-enforcement (RULED 2026-07-15, survives narrowed):** policies emit at natural
  severity; enforcement overrides after collection. But the map's checked-in surface was the lint half —
  for guardrails the layering is defaults < flag/env, and `destructive_action` stays out of checked-in
  config. The old recorded tension evaporated: lint and guardrails never shared a surface.
  `WithDestructiveActionPolicy(PolicyEnforcement)` survives as sugar.
- **Rule discovery (survives, load-bearing):** the seam declares its rules. Powers `policy list` and
  makes a typo'd id a diagnostic instead of silence.

## Open: the exception story

The unsolved piece, and the reason project-policy config is TBD.

- The deliberate no-PK append-only table has no file to scope an editorconfig section to — the finding is
  about the model, not the text. Whatever the escape is (policy parameters, config-in-SQL, something on
  the declaration itself), it isn't editorconfig, and it's undesigned.
- Not every rule needs one: nullable-PK and duplicate-columns are contradictions with no legitimate
  exception. Design the escape only for rules that have legitimate exceptions.

## Open questions

1. The exception mechanism for project policies.
2. The plugin seam shape for project/plan rules (and whether it drives the qualified `PLUGIN` form).
3. Rename for `SchemaLintPolicy`.
4. Do plan policies ever need per-rule config beyond the existing enforcement options?
5. **`Source` vs rule-id prefix** (carried from the old doc, still non-retrofittable): `Source` stays policy
   identity, but dot ids make the prefix look like a category — and the spellings collide (`schema-lint`
   kebab vs `schema_lint` underscore). Decide whether `Source` respells to match, or the id prefix and
   `Source` are simply never the same thing.

ErrorOr's wider lessons — factories, implicit conversions, `Match`/`Switch` — shipped with the
result-ergonomics work. Only the rule-id half landed here.
