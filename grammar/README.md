# NSQL TextMate grammar

Syntax highlighting for NSQL. One grammar, several consumers: JetBrains IDEs, VS Code, and the Shiki
highlighter behind the docs site.

- `nsql.tmLanguage.json` — the grammar. Scope `source.nsql`, file type `.nsql`.
- `language-configuration.json` — comment toggling, brackets, auto-closing pairs.
- `package.json` — the bundle manifest, in the VS Code layout that JetBrains also reads.

## Why it lives here

The grammar states the language a second time, in regex, for editors that cannot run the parser. Keeping it
beside the parser is what lets `NsqlGrammarTests` hold the two together as an ordinary unit test: every
keyword in `NsqlKeywords` must be highlighted, and every word highlighted must still be a keyword. Anywhere
else, that test would need a package reference to Core and would drift against the version under test.

## Using it

**Rider / DataGrip** — Settings → Editor → TextMate Bundles → `+` → select this directory. No plugin needed.

**VS Code** — copy or symlink this directory into `~/.vscode/extensions/nsql/` and restart. Bodies highlight
as SQL there, since VS Code ships a `source.sql` grammar.

**Docs site** — read `nsql.tmLanguage.json` into Astro's `shikiConfig.langs`, then fence examples as `nsql`.

## Choices worth knowing

- **Types are not highlighted.** `bigint`, `text`, `varchar(255)` are provider vocabulary, not language
  vocabulary — NSQL is dialect-agnostic, so the grammar has no type list to go stale.
- **A declaration's name is `entity.name.type`; every other delimited name is `variable.other`.** The
  declaration site is the one place a name is being *given* rather than used.
- **Script and routine bodies embed SQL.** Where a host has no `source.sql`, the body still reads as one
  region with its literals and comments intact — see the note on the `bodies` rule for why the fallbacks
  are load-bearing rather than decorative.
- Configuration statements are anchored to the start of a line. `state`, `path` and `version` are common
  column names, and an unanchored rule would label whatever followed one.

## Changing it

The unit tests cover the vocabulary, the patterns' validity, and this manifest — not tokenisation. To check
real output, run the grammar through the engine the editors use:

```sh
npm install vscode-textmate vscode-oniguruma
# load nsql.tmLanguage.json into a textmate.Registry, tokenizeLine over a sample, print token.scopes
```

Check a sample twice: once with a `source.sql` grammar registered and once without, because an include that
resolves to nothing takes its whole rule down with it.
