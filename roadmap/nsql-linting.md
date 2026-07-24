# NSQL linting

Lint and formatting for NSQL, configured through literal `.editorconfig` support. The style half of what
was "custom policies" — the correctness/guardrail half stays in [[custom-policies]].

- **Repo:** Core (lint seam, rule identity, built-in rules), new `NSchema.EditorConfig` package (config
  reading, dependency-needing rules), CLI (wiring, `fmt`/lint output).
- **Designed by working forwards from developer experience (2026-07-20)**, not backwards from the existing
  policy plumbing. Several old rulings re-derived with better arguments; others fell over. See Supersedes.

## The layer ruling (2026-07-20)

**Lint runs at the syntax layer, per document — not against the domain model.**

- Linting is about the *text*: naming, preferred syntax, directive style. Project/plan policies are about
  the *declared state* and the *change*. What a rule is about determines its layer.
- The layer already has everything lint needs: `NsqlNode` carries a required `SourcePosition`, and
  `NsqlDiagnostic` carries file + position. Findings are born addressable — `file:line` output for free.
- It makes unaddressable things lintable: `TEMPLATE` / `SCRIPT` directives, preferred syntax — none of
  which survive into the model.
- The address→file "source map" a model-level linter would need is not solved but *dissolved*: it was an
  artifact of linting at the wrong layer.
- `SchemaLintPolicy` contains **zero lint rules** — all four checks are correctness (no-PK is only
  answerable at the model; a PK can be declared in a different file than its table). It stays an
  `IProjectPolicy` and wants a rename. Details in [[custom-policies]].

## The config surface: literal `.editorconfig` (2026-07-20)

Not an analogy, and **no lint grammar in NSQL** — no `POLICY` statement, no `SUPPRESS` directive, no
comment pragmas. Linting doesn't earn language surface.

```ini
[*.sql]
nschema_diagnostic.naming.tables.severity = error
nschema_naming.tables.form = plural

[legacy/**.sql]
nschema_diagnostic.naming.tables.severity = none
```

- **Roslyn precedent:** `dotnet_diagnostic.<id>.severity` is exactly "a domain tool adding its own
  property namespace to editorconfig". Parse by prefix/suffix, so dots inside the id are unambiguous.
- **Layering is inherited, not designed:** `root = true`, directory nesting, closest-wins. CLI flags on top.
- **Suppression is path-scoped sections.** NSQL has no file semantics (an index can live in a different
  file than its table) — and at the syntax layer that's fine: a rule fires at the statement *where it's
  written*, and the section matching that file governs it. The only coherent scope for style.
- **Unknown keys:** the ecosystem convention is that tools ignore keys they don't own (the file is shared).
  We validate strictly under the `nschema_` prefix only — which makes rule discovery on the seam
  load-bearing, not optional: a typo'd rule id in our namespace must be a diagnostic, not silence.
- Guardrails never touch this file. `destructive_action` stays flag/env — the old recorded tension
  evaporated because lint and guardrails never shared a config surface in the first place.

## Severity + rule identity (ruled 2026-07-20)

- **Adopt the .NET editorconfig severity vocabulary:** `error` / `warning` / `suggestion` / `silent` /
  `none`. `PolicyEnforcement` maps onto it; we're close already. Open nuance: whether a CLI collapses
  `silent` into `none` (accept both spellings regardless).
- **Rule ids are dot-separated slugs**, underscore words: `naming.tables`. Identical everywhere — terminal
  output, config key, docs — so the DX is "copy the thing on the screen into the config".
- Slug over opaque code: we're CLI-first and the config is hand-written; `NS0001` only works when an IDE
  mediates. Renames become breaking — priced, same as config keys.

## Naming rules

The first *actual* lint rules (nothing shipped today qualifies).

- Case/pattern rules per object kind: mechanical, parameters in editorconfig keys.
- **Plural table names: yes, via `Humanizer.Core`.** The wordlists are the hard part — irregulars,
  uncountables, suffix ordering — and hand-rolling means re-deriving them badly. Its vocabulary is
  runtime-extensible, which is the user exception list.
- The linter bargain, stated in docs rather than hidden: English-only (don't enable it otherwise), mass
  nouns need the exception list, default severity `suggestion` so a misfire annoys rather than blocks.

## Formatting

- In scope — formatting is editorconfig's home turf. Honor the standard properties (`indent_style`,
  `indent_size`, `end_of_line`, `insert_final_newline`) plus `nschema_` style keys.
- The formatter is better than remembered (comment attachment, blank-line capping) but is a token
  stream. **Token-level rules ship first:** keyword case is a pure token transform, indent/newline
  properties replace hardcoded constants.
- **Structural reformatting** (wrapping column lists, aligning) needs a lossless tree — the CST rework
  has shipped, so it's unblocked.
- Division of labor, per `dotnet format`: auto-fixable style lives in the formatter, non-fixable
  convention (naming) lives in lint, editorconfig governs both.

## Packaging (lean, 2026-07-20)

- **Rule: Core-the-engine stays dependency-free; the language/tooling layer is the dependency-tolerant
  zone.**
- Core defines the seam: the lint rule interface, rule identity, discovery, and a resolver contract from
  `(rule id, file)` to severity/parameters.
- **`NSchema.EditorConfig`** owns reading the format (INI + the spec's glob semantics) *and* the
  dependency-needing rules (Humanizer-backed naming) — it's the lint distribution package, not just a
  reader. CLI references it; programmatic Core users wire the resolver or get natural severities.
- Registration is compile-time via the builder — **not** the ALC plugin machinery. Custom lint rules via
  plugins are deferred; the plugin seam belongs to [[custom-policies]].

## Supersedes

- **`POLICY` statement in config-in-SQL (sketched 2026-07-09)** — superseded 2026-07-20 by literal
  editorconfig. Config-in-SQL carries no lint config.
- **`SUPPRESS` directive / inline pragmas (floated 2026-07-20)** — dead on arrival; path-scoped sections
  cover suppression without language surface.
- **Model-level linting** — the old doc's motivating example was an `IProjectPolicy`; the layer ruling
  reclassifies it. Survived re-derivation with *stronger* arguments: slugs, rule discovery,
  diagnostic-carries-id.

## Open questions

1. Seam shape: node visitor vs whole-document pass? (Formatting-flavored rules want token access —
   where's the line between linter and formatter input?)
2. `silent` vs `none` in a CLI.
3. First rule set: which object kinds, which parameters.
4. Second file extension (`.nsql`?) pairs well here — editorconfig sections key on globs, and editors
   key grammars on extensions (see ROADMAP "Editor support").
