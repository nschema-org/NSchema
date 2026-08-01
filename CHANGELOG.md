# Changelog

All notable changes to NSchema will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Versions before 3.0.0 covered the library-only era of NSchema. They are kept for historical reference only.

## [5.0.1] - 2026-08-01

### Fixed

- **Policy enforcement ignored on apply/destroy.** Running a plan or apply will now correctly respect policy enforcement adjustments, so destructive actions can be made allowed.

## [5.0.0] - 2026-08-01

v5.0 is a Core rearchitecture, aiming for better project health, with clear separation between layers and a better separation of concerns.

### Changed

- **Any policy's findings can be configured, not just the built-in ones.** `WithDiagnostic(code, enforcement)` configures one finding and `WithDiagnosticsFrom(source, enforcement)` a producer's whole family, replacing `DestructiveActionOptions` and `DataHazardOptions` — a policy no longer needs its own options type and a builder method to be configurable, so a policy you register is configurable too. A policy reports at the severity it judges natural and the engine applies the override, so `WithDestructiveActions` and `WithDataHazards` remain as named shortcuts for the two built-ins.
- **A diagnostic's `Source` is what produced it, and only that.** The differ reported findings under four topics (`scope`, `directives`, `run-once`, `data-migrations`) where the producer was the differ; those topics now live in the code, which is where a finding's identity belongs.
- **Column alterations are unified.** Dialects now receive a single `AlterColumn` action for a column's type and nullability changes, which they can render as one or more statements.
- **Member diffs are built by factory.** `PrimaryKeyDiff`, `ForeignKeyDiff`, `UniqueConstraintDiff`, `CheckConstraintDiff`, `ExclusionConstraintDiff`, `IndexDiff` and `TriggerDiff` expose `Added(definition)`, `Removed(name)` and `CommentChanged(name, change)` in place of a constructor.
- **Object diffs are built by factory too.** Every schema-level diff — table, column, view, enum, domain, sequence, routine, composite type, extension and schema is constructed through `Added` / `Removed` / `Modified` and refined with `with`, replacing constructors.
- **Better domain mode naming.** `Database` and `Schema` are now the main entry points into the domain model.
- **The SQL dialect seam is the abstract `SqlDialect` class.** A dialect overrides one method per migration action. Standard SQL is rendered by the base through an overridable identifier-quoting kernel.
- **Identifiers are case-sensitive.** An identifier's identity is its exact written text: `users` and `Users` are no-longer considered equivalent.
- **Identifiers can be quoted.** `CREATE TABLE app."Order Details" ("weird ""col""" int)` all work, and lets a name collide with a keyword. Square brackets are accepted as an alternative spelling, so `CREATE TABLE app.[Order Details] ([weird ]]col]]] int)` reads the same (`]]` escapes a bracket). Quotes and brackets are syntax, not identity: casing is significant with or without them, and either spelling names the same object. The writer (and import) quotes only names that need it, always with double quotes, and extension names now render as quoted identifiers rather than string literals.
- **Parsing is lossless.** The syntax tree now preserves every character of the source, including comments, whitespace, and layout.
- **Management directives.** The language now separates *declarations* (what the schema is) from *directives* (how the difference is managed). This includes RENAME and SCRIPT.
- **Every namespace has moved.** Namespaces are vertically sliced of the form `NSchema.<Feature>.<Capability>`.
- **A slice's domain layer is `Domain`, not `Model`.**
- **The shared primitives live in `NSchema.Diagnostics`.**
- **The schema model is `NSchema.Model` now.** It owns the top-level domain model for databases.
- **The model is layered by what contains what.** A `DatabaseObject` belongs to the database, a `SchemaObject` to a schema, and an `ObjectMember` to a schema object.
- **An element's `Address` is never null.** A schema object addresses itself as an `ObjectAddress` and a member as a `MemberAddress`, so neither needs narrowing or a cast.
- **A kind belongs to one level.** `DatabaseObjectKind`, `SchemaObjectKind` and `MemberKind` replace the single `ObjectKind`, which spanned schemas' contents and the database's own.
- **An address is named for where it sits, not for what it names.** `DatabaseAddress` replaces `SchemaAddress` and carries the kind as data (`DatabaseAddress.Schema(name)`, `DatabaseAddress.Extension(name)`), so a new database-level kind needs no new address type. `MemberAddress` takes an optional kind, matching `ObjectAddress`.
- **A script is identified, not addressed.** A script decorates the schema rather than living in it, so `ScriptReference` replaces the address it used to carry and `Script.Reference` replaces `Script.Address`. A script's scope is a schema name rather than an address, and the run-once ledger keys on the reference. With extensions addressed as the database objects they are, no address carries an optional schema any more.
- **The diff interfaces name the same levels as the model.** `IDatabaseElementDiff` replaces `INamedObjectDiff` as what every diff node shares, and `IObjectMemberDiff` is the member level — so an index and a trigger diff sit with the columns and constraints rather than alongside an extension. `IMigratableDiff` narrows to a member diff that can carry a script, and `TableDiff.EnumerateMembers()` returns the member abstraction.
- **A diff node's `Change` replaces its `Kind`.** `Kind` now means what sort of thing something is, so the diff says what happened to it instead: `diff.Change == ChangeKind.Add`.
- **`ChangeKind.Touched` replaces a null change.** A schema in the diff only because its contents changed is `Touched` rather than carrying no change at all, so `SchemaDiff.Change` is always present and `IDatabaseObjectDiff` covers both schemas and extensions. `Touched` is the enum's default, so an unset change reads as "unchanged" rather than "create this".
- **DataMigrations are Scripts now.** This reflects the syntax changes introduce in [4.4.0] so the model becomes consistent.
- **Templates accept object-level directives.** A `TEMPLATE` body may now contain the object-level `RENAME` directive (table, column, view, enum, domain, type, sequence, routine) alongside its declarations and scripts.
- **Scripts split into `ChangeScript` and `DeploymentScript`.** `Script` is now an abstract base carrying the common behavior (name, SQL, scope, hash, reference, run condition);
- **"Desired" is Project now.** `IDesiredSchemaProvider` becomes `IProjectProvider` — the project is the desired state by definition, and `app.DesiredSchema` becomes `app.Project`, named for what it hands you like every surface beside it. Progress output follows: `Loading project...`, `Validating project...`, and the plan census now reads `Declared:` / `Recorded state:`.
- **`AddDdlSchemas` is `AddProjectSource` now.** The files describe the whole project (schema, scripts, templates, config), not just schema DDL.
- **Result<T> use consistency.** Lots of interfaces have been neatened up to return a `Result<T>` instead of throwing to allow for error/warning accumulation.
- **The diff now includes scripts.** Rather than being tacked on to the plan, scripts are now a first-class part of the diff, carried where they run rather than in a central list.
- **Cohesive plan artifact.** There's now a single `MigrationPlan` model that represents the plan in its entirety rather than being spread across `SqlPlan`, `PlannedMigration`, etc.
- **Providers are required.** Providers are now required for planning, because the SQL is built into the plan model.
- **Plan errors are non-blocking.** Even when the plan has errors, you can now still access the resultant plan.
- **Planning always diffs recorded state against the project.** There is no longer an option to plan against the live database.
- **A teardown is a plan towards an empty schema.** `PlanTarget.Empty` replaces `PlanTarget.Teardown`, and it obeys `PlanArguments.Scope` like any other plan.
- **Teardown plans run the policies.** A teardown is fully destructive, so the default destructive-action policy blocks it; the blocked result still carries the complete plan. Set the destructive-action enforcement to `Allow` to apply one.
- **Policies are enforced at apply.** `Apply` now re-runs all policies against the plan diff before executing.
- **Policies now cover project and plan.** `IProjectPolicy` replaces `ISchemaPolicy` and `IPlanPolicy` replaces `IDiffPolicy`.
- **`SqlDialect` replaces `ISqlGenerator`.** (registered with `UseSqlDialect<T>()`).
- **`IStateLockManager` replaces `IStateLockCoordinator`.** Lines up with with `ISchemaStateManager`.
- **`IPlanFileManager` replaces `IPlanFileWriter`.** It reads saved plans too, so "writer" undersold it.
- **`UseFileState` replaces `UseFileStateStore`.** It registers the lock alongside the store, so "store" undersold it — matching `UseEphemeralState`, which does the same. `UseStateStore` and `UseStateLock` still register a single seam each.
- **`TableDiff.PrimaryKeys` replaces `TableDiff.PrimaryKey`.** It has always been a list — replacing a key is a remove *and* an add — so the singular name misread as one change.
- **The serialization types live in `NSchema.Model.Serialization`.** They move out of `NSchema.Model.Services`, which is for services over the model.
- **`options.AddModelConverters()` composes the model's JSON conventions.** A consumer serializing NSchema types adds the converters to its own `JsonSerializerOptions` rather than naming them — persistence wants indented, complete output and a console stream wants terse single lines, so only the conventions are shared. The converters themselves are internal now.
- **`IDatabaseIntrospector` replaces `ISchemaProvider`.** More honest about what it does now that the interface doesn't serve both the current and desired sides, and named for what it returns.
- **Plugin `Configure` returns `Result`.** Configuration errors are diagnostics like everything else.
- **Opaque SQL is `SqlText` now.** Every schema-model field carrying SQL that NSchema stores verbatim but does not interpret is typed `SqlText` instead of `string`.
- **A qualified type's schema is a component now.** `SqlType` carries the schema of a user-defined type (e.g. `app` in `app.order_status`) as a structural `Schema` property rather than folded into its nam.
- **`PolicyEnforcement` absorbs the `DestructiveActionPolicy` enum.** The shared enum gains `Ignore`.
- **`WithDestructiveActions` and `WithDataHazards` replace `WithDestructiveActionPolicy` and `WithDataHazardPolicy`.** They set how a built-in policy is enforced, which the old names read as registering one — `AddPlanPolicy<T>()` is what registers.
- **The state ledger field is `scripts` now.** Pre-5.0 `executedScripts` payloads read as an empty ledger. Refresh (or untaint) existing state under the state-format compatibility policy's major-version rules.
- **Configuration is part of the language.** `ENGINE`, `PLUGIN`, `DATABASE` and `STATE` parse as ordinary statements, in the one grammar that carries declarations and directives. Which statements a given file *should* hold is a consumer's rule, not the parser's: each subsystem picks out the statements it understands and ignores the rest.
- **`DATABASE` and `STATE` replace `PROVIDER` and `BACKEND`.** Each names the thing it configures rather than the role that supplies it.
- **Every setting is environment-overridable.** `NSCHEMA_<KEYWORD>_<SETTING>` overrides one setting on the matching statement — `NSCHEMA_DATABASE_CONNECTION_STRING` sets `connection_string` on `DATABASE` — replacing the written value or supplying one the statement omits, so a secret need not be committed. The keyword scopes the name because `DATABASE` and `STATE` may both take a setting of the same name.
- **Plugins receive `PluginSettings`.** `Configure` takes the statement's label and setting values as a flat key/value map (`settings.Values`, or `settings.Value(name)` for one), translated from the parsed statement by the configuration assembly. `settings.Get<T>()` binds them onto an options type — snake_case keys match properties, dotted keys nest, identifiers map to enum members — and reports an unbindable value, an unknown setting, and a failed `[Required]`/`[Range]` as error diagnostics, so a plugin declares its options rather than parsing them.
- **`INSchemaDatabasePlugin` and `INSchemaStatePlugin` replace `INSchemaProviderPlugin` and `INSchemaBackendPlugin`.** Each is named for the statement that configures it.
- **Plugins are resolved by capability, not by name.** `INSchemaPlugin.Label` is gone — the statement kind selects the capability interface, and the label in configuration is the user's local name for a declared `PLUGIN`, never the plugin's own. `ScaffoldContext.Version` is gone with it: the host authors the `PLUGIN` statement (it knows the package and the resolved version), so a plugin's scaffold template contributes only its own configuration statement.
- **`NsqlReader` replaces `DdlReader` and diagnostics are structural.** `NsqlReader.Read`/`ReadFile` return `Result<NsqlDocument, NsqlDiagnostic>`, the new diagnostic-typed result, with each finding carrying its source position.
- **`DdlReader.Read` returns `Result<DdlDocument>`.** A syntax error is an error diagnostic instead of a thrown exception, and the parser now recovers at statement boundaries.
- **`DatabaseSchema` is pure data now.** `Filter` joined `Combine` off the model, into the projection machinery.
- **`DiffDocument.From(diff)` replaces `DiffReader.Read(diff)`.** The reader type and its `.Default` singleton are both gone, and the namespace is `NSchema.Diff.Rendering`: "reader" named the wrong direction, since a `DatabaseDiff` is projected into display lines rather than parsed from anything.
- **`SchemaScope` replaces bare schema-name arrays.** `GetProject`, `GetSchema`, and the plan/drift/import arguments take a scope record.
- **State locks receive complete metadata.** `IStateLockManager.Acquire` takes `AcquireLockArguments` with the operation, TTL, and skip-lock; it creates the `StateLockInfo` atomically recorded by `IStateLock.Acquire`.
- **Project reads report every broken file at once.** An unreadable or unparseable file (and no-files-matched) is an error diagnostic on the project.
- **Index keys and exclusion elements are column-or-expression now.** `IndexColumn` and `ExclusionElement` carry mutually exclusive `Column` (an identifier) and `Expression` (verbatim SQL) properties.
- **References are value objects now.** `Trigger.Function` carries a `RoutineReference` (optionally schema-qualified; unqualified resolves via the engine's search path)
- **`ObjectAddress` addresses a schema-level object.** Always fully qualified. Each part compares as an identifier. State and plan-file payloads serialize the address structurally.
- **Object names are `SqlIdentifier` now.** Every name-bearing property across the schema, diff, plan, and state models carries a value object.
- **Template migrations are decoupled from their tables.** Migrations can be declared in any template for any table.
- **Scripts execute as woven statements.** The linearizer weaves the diff's scripts into the ordering so scripts are now first-class actions.
- **Planning and applying now require a state store.** Use the new ephemeral store if you need to run without persistent state for CI or integration tests.
- **State records what NSchema manages.** Alongside the full observation and the run-once ledger, state carries the managed identity set: what an apply has created or adopted. A plan only ever covers managed/declared objects, so an object missing from the project only triggers a drop when the state contains a *managed* object, and objects outside the managed state are never touched.
- **Teardown destroys what NSchema manages.** `Plan(Empty)` converges the managed set — not everything ever observed — towards nothing.
- **Managed extensions honor removal-by-absence.** A declared extension becomes managed on apply and is dropped when un-declared.
- **A foreign key into an undeclared table is a warning, not an error.** The target may exist unmanaged (gradual adoption), so the structural policy advises instead of blocking.
- **Constraints fold into table creates.** A newly-created table's foreign keys, unique and check constraints are now rendered inline in its `CREATE TABLE` rather than as trailing `ALTER TABLE ADD CONSTRAINT` statements.

### Added

- **Enforcement that cannot be honored says so.** Configuring a structural finding to be reported more leniently reports `cannot-be-lowered` rather than quietly doing nothing, once per finding however many times it occurs.
- **A finding says whether it may be overruled.** `DiagnosticKind` marks a finding as `Structural` — NSchema cannot do what was asked, so silencing it would produce something wrong rather than something permitted — or `Advisory`, a judgement the caller is entitled to overrule. Enforcement can raise any finding, but can only lower an advisory one, at whatever severity it currently carries. Reporting below error severity declares a finding advisory, so a warning or an informational finding is silenceable by default and an error is not — and because the kind is fixed when the finding is created, raising one later cannot change what a caller is allowed to silence.
- **A diagnostic's source is a `DiagnosticSource`.** The producer's name is typed rather than a bare string, and holds to the same shape as a code — a user groups and configures by it, so it has to be usable as a settings key too.
- **A diagnostic carries a code.** `Diagnostic.Code` names a finding independently of how it is worded, so a message can be reworded without breaking anything that refers to it. A code is restricted to hyphen-separated lowercase words, so it is usable as a settings key, and an invalid one is rejected rather than rewritten. Every code is unique across NSchema — the producer is not always known at compile time, so the code alone addresses a finding.
- **A diagnostic collection folds to a result.** `ToResult()` and `ToResult(value)` move onto `DiagnosticCollection<TDiagnostic>`, so accumulating findings and returning them needs no collector. A collection can also be built from a collection expression.
- **A typed result lifts like a plain one.** `Result<TValue, TDiagnostic>` converts implicitly from a value and from a single diagnostic, and a value-less `Result` converts from a diagnostic — so a method returns one directly instead of naming the result type to construct it.
- **`SqlDialect.CanAlterForeignKeys`.** A dialect states whether a foreign key can be added to, or dropped from, a table that already exists. One that cannot (SQLite) keeps every key on the `CREATE TABLE` that declares it, and the plan never separates one from its table.
- **The comparison seam is the `SqlEquivalence` class.** A provider can register (`UseSqlEquivalence<T>()`) one equality comparer per comparison context, deciding when two spellings mean the same thing. The neutral base compares types structurally and defaults on cosmetics-normalized text.
- **Default expressions are `SqlDefaultExpression` now.** `Column.DefaultExpression` and `DomainType.Default` graduate from `SqlText` to their own value object, so equivalence rules register against the specific context.
- **Change-script targets are value objects.** `ChangeScript` now takes a `ChangeTarget` instead of separate trigger and path values.
- **Atomic table-fragment merge.** `Table.TryMergeMembers` applies a complete table-member fragment or reports its conflicts.
- **SQL type conversion risk.** `SqlType.ConversionRiskTo` assesses whether converting stored values can fail.
- **Ephemeral state.** `UseEphemeralState()` registers an in-memory state store, which serves as the lock too, intended for disposable databases.
- **Object-granular targeting.** `PlanningScope` covers a single list of `Address`es (`PlanningScope.To(addresses)` / `scope.Addresses`), mixing whole-schema and object-level targets.
- **Address parsing.** `NsqlReader.ReadAddress` parses a `schema`, `schema.object`, or `schema.object.member` fragment into an `Address`, resolving quoted segments
- **Address containment.** `Address.Covers(other)` expresses downward containment (a schema covers its objects and members; an object covers its members).
- **Scope and identity are address-based.** `PlanningScope` is scoped when it holds any address (schema or object).
- **`ObjectAddress` carries an optional `Kind`.** A null kind addresses every kind sharing the name (kind-free targeting); a set kind disambiguates same-named objects.
- **`Token.QuotedIdentifier`.** Synthesizes a quoted-identifier token (decoded text, quoted-and-escaped raw), the counterpart to `Token.StringLiteral`.
- **Configuration statements are built by factory.** `SettingsStatement.Database(label)` / `.State(label)` / `.Plugin(label)` / `.Engine()` / `.Lock()` replace the constructor, refined with `WithSetting(key, value)`, `WithDocComment(text)` and `WithLeadingComment(text)`.
- **A plugin declares its scaffolding questions.** `INSchemaPlugin.GetScaffoldPrompts(context)` returns the `ScaffoldPrompt`s a front-end should put to the user, and the answers arrive on `ScaffoldContext.Answers` for `GetScaffoldTemplate` to build its statement from.
- **`PLUGIN` declares plugin dependencies.** `PLUGIN <label> ( source = '…', version = '…' );` separates dependency declaration from configuration.
- **`ENGINE` asserts the engine and/or host version.** `ENGINE ( version = '…', host_version = '…' );` — `version` is checked against the engine (Core), `host_version` against the host tool.
- **The engine handshake.** `PluginHandshake.Validate` checks a loaded plugin assembly against the hosting engine before any of its types are instantiated.
- **`ConfigurationProvider.Load` loads a configuration.** One call from ordered configuration *layers* (a later layer overrides an earlier one) to a validated `ConfigurationDefinition`.
- **The plugin lockfile.** `LockFileManager` reads and writes `nschema.lock` (a `LOCK ( source = '…', version = '…' );` grammar) as a `LockFile` of `LockedPlugin` pins. `LockFile.Resolve(declaration)` resolves a declaration against the lock.
- **`VersionRange.IsExact` / `ExactVersion` / `Highest`.** Report whether a range pins a single version and the version it pins, and select the highest of a supplied set of versions that the range admits.
- **Plugins split from configuration.** The provider interfaces stay in `NSchema.Plugins` (`INSchemaPlugin` and friends, the handshake, `ScaffoldContext`); everything a project *declares*, now lives under `NSchema.Configuration`. `PluginSettings` is in `NSchema.Configuration.Plugins`.

### Removed

- `Address.SchemaName`. An address answers where it sits through its own components.
- `PRE|POST DEPLOYMENT '<name>' AS $$…$$;` and `MIGRATION ['<name>'] FOR <event> <path> AS $$…$$;` no longer parse.
- The `DROP` statements (`DROP SCHEMA|TABLE|VIEW|ENUM|DOMAIN|TYPE|SEQUENCE|FUNCTION|PROCEDURE|ROUTINE|EXTENSION`) no longer parse. Remove the declaration instead.
- `CREATE PARTIAL SCHEMA` no longer parses. The managed identity set covers what it was reaching for: an object the state does not record as managed is never dropped, declared or not.
- `RENAMED FROM` clauses no longer parse. A rename is a `RENAME` directive now (see Management directives above).
- The `NSCHEMA` configuration statement no longer parses.
- `DataMigration` has been folded into `Script` and now requires a name for so they can maintain a stable identity.
- **Narrowed public surface.** A variety of types that should never have been exposed have been made internal.
- `PolicyDiagnostics`, `PluginConfigureResult`, and the `DestructiveActionPolicy` enum — all made redundant by first-class severity on `Result` and the shared `PolicyEnforcement`.
- `DdlSyntaxException`, `PlanFileDeserializationException`, and `StateDeserializationException` are now internal; the read seams surface these failures as diagnostics.
- `MigrationAction.IsDestructive` has been removed. Destructiveness is judged from the diff by `DestructiveActionPolicy`, not per action.
- `ViewDiff.DependsOn` has been removed.

### Fixed

- **Tables are created and dropped in foreign-key order.** A table is now always created after the tables it references and dropped before them, across schemas, so a teardown no longer fails on a table another one still points at. Where tables point at each other no order can satisfy every key, so the ones it cannot are moved out of the way: on a drop the constraint goes first, and on a create it is added once both tables exist rather than riding the `CREATE TABLE`. A dialect that cannot alter foreign keys says so with `SqlDialect.CanAlterForeignKeys`, and keeps every key on the table that declares it.
- **Schemas are no longer created implicitly.** Creating an object without also creating its schema will no-longer auto-adopt the parent schema.
- **Adoption-only plans now count as non-empty.** Running a plan that changes nothing, and only imports objects into the managed set now correctly reports as having changed.

## [4.6.1] - 2026-07-10

### Changed

- `RUN ONCE` deployment scripts that have already been run no-longer show up as an informational diagnostic.

## [4.6.0] - 2026-07-10

### Added

- **Public desired-schema access.** `IDesiredSchemaProvider` is now public and exposed as `app.DesiredSchema`.
- **`Hash` on `Script` and `DataMigration`.** For reading the canonical hash of the body.

### Changed

- Refresh no longer silently replaces a state payload it cannot read: it fails unless `RefreshArguments.Force` is set, and a forced replacement carries
  a warning that the run-once script ledger was reset. The state capture after an apply still replaces an unreadable payload (the SQL has already run),
  with the same warning.

## [4.5.0] - 2026-07-10

### Added

- **Public state access.** The recorded state is now a public model.
- **`ISchemaStateManager`**, exposed as `app.State`, facilitates reading and writing the recorded state, with an optional `ReadRaw`/`WriteRaw` methods for moving the serialized payload without interpreting it.

### Changed

- Planning with an unreadable state payload now fails with a diagnostic instead of throwing.

## [4.4.0] - 2026-07-10

### Added

- **Unified `SCRIPT` statement.** `SCRIPT '<name>' RUN [ALWAYS | ONCE] ON <event> AS $$…$$;` is the new canonical form of deployment scripts and data migrations.
  The event is a deployment bookend (`PRE DEPLOYMENT` / `POST DEPLOYMENT`) or a structural change (`ADD COLUMN` / `ALTER COLUMN TYPE` / `ADD CONSTRAINT` with a target path);
- `RunCondition` on scripts and data migrations, carrying the parsed `RUN` clause.
- The backend state store now carries the recorded script executions.
- A `RUN ONCE` script is recorded on a successful apply and skipped by later plans. A recorded script whose body has since changed stays skipped and warns.
- **Migrations in schema templates.** A `MIGRATION` block can now be declared inside a `TEMPLATE … BEGIN … END;` body with an unqualified `table.member` path; applying the template instantiates the block once per target schema. The `{schema}` token in the block's SQL is replaced with each target schema's name.

### Changed

- Script names must now be unique across the project (they identify scripts in diagnostics and run-once tracking). A named block declared in a template applied to multiple schemas can include the `{schema}` token in its name to keep instances distinct.
- `DdlWriter` renders deployment scripts and named data migrations in the `SCRIPT` form; anonymous migrations keep the legacy spelling.

### Deprecated

- The `PRE|POST DEPLOYMENT '<name>' AS $$…$$;` and `MIGRATION ['name'] FOR <trigger> <path> AS $$…$$;` forms. Both still parse into the same model, and plan/apply/validate now surface a `deprecations` warning naming the `SCRIPT` replacement. They will be removed in NSchema 5.0.

### Fixed

- The formatter no longer re-indents the interior of a dollar-quoted body inside a `TEMPLATE` block, which grew the indentation on every pass and changed the SQL a routine or migration body carries.

## [4.3.1] - 2026-07-09

### Fixed

- The diff now shows an added or removed column's default expression and identity marker, so a column definition reads the same everywhere it appears.
- DDL syntax errors now name the file the error was found in, alongside the existing line and column.
- Import no longer repeats the `CREATE SCHEMA` statement in every object file; only the per-schema header declares the schema.
- Import now writes the per-schema header to `<schema>/schema.sql` instead of `<schema>.sql`.

## [4.3.0] - 2026-07-09

### Added

- **Data migrations.** A `MIGRATION ['name'] FOR <trigger> <schema>.<table>.<member> AS $$…$$;` block attaches raw SQL to a structural change (`ADD COLUMN`, `ALTER COLUMN TYPE`, or `ADD CONSTRAINT`) and is spliced into the plan only when the matching change is planned.
- **Decomposed NOT NULL adds.** A required column add with no default and a matching `FOR ADD COLUMN` migration is planned as add-nullable → run the migration SQL → `SET NOT NULL`, so the add succeeds against a populated table.
- **Hazard suppression.** A matching migration block silences the corresponding data-hazard diagnostic (per trigger; unique-index hazards have no trigger and are never suppressed).
- **`ExecuteDataMigration`** plan action carrying the spliced SQL. Executing a plan containing one requires a provider that recognizes it (4.3+); plans with no matched blocks are unaffected.

## [4.2.0] - 2026-07-09

### Added

- **Data-hazard detection.** A new built-in diff policy flags planned changes that are valid against the schema but can fail on the data already in a table.

## [4.1.0] - 2026-07-08

### Added

- **Schema template declaration.** A `TEMPLATE name BEGIN … END;` block allows you to declare a reusable group of objects once.
- **Schema template application.** A `APPLY TEMPLATE name IN SCHEMA a, b;` statement instantiates a template into each named schema.
- **Table templates.** A `TEMPLATE name FOR TABLE BEGIN … END;` block declares reusable table members.
- **Index name validation.** Duplicate index and index-backed constraint names (primary key, unique, exclusion) within a schema are now rejected at validation time.
- **`DdlDocument.Templates`** carries the parsed template constructs for consumers of the reader — a `TemplateSet` of definitions, applications, and includes.
- The formatter lays out `TEMPLATE` blocks canonically: header and `BEGIN`/`END` on their own lines, inner statements formatted as usual and indented one level.

## [4.0.1] - 2026-07-06

### Added

- **`ViewDiff.Materialized`.** A plain ⇄ materialized view conversion is now carried explicitly on the diff as a `ValueChange<bool>`, and `DiffReader` renders it as a label transition (`view → materialized view app.name`).

### Fixed

- **Renamed tables.** Constraint, index, and trigger drops (and privilege revokes) on a table being renamed now target the old table name, and execute before the rename.
- **Renamed schemas.** The schema rename is now ordered ahead of every other action, so drops and revokes inside the renamed schema resolve correctly.
- **Renamed materialized views.** In-place index drops on a renamed view target the old view name. A rename accompanying a recreate no longer emits a `RenameView` (which would have targeted a dropped view); the drop removes the old name and the recreate creates the new one.
- **View conversions.** A plain ⇄ materialized conversion now drops the view as what it currently is, instead of using the desired side's materialization for the drop.

## [4.0.0] - 2026-07-01

v4.0.0 is a major release that reworks providers and backends into a new plugin system. This will enable providers and backends to be installed directly from NuGet, independently of the CLI, and pin the versions in your CI.

### Added

- **Plugin Contract.** A new set of interfaces in `NSchema.Plugins` that will allow providers and backends to declare themselves.
- **BREAKING: Manual lock holds.** `IStateLockHandle` is no longer disposable; release is explicit, so a caller can take a lock that outlives the current process (e.g. one front-end command acquires, another releases) simply by never releasing it.
- **Lock time-to-live.** `StateLockRequest.TimeToLive` records an optional expiry on the resulting `StateLockInfo.ExpiresUtc`. Expiry is surfaced for visibility but never auto-enforced.
- **BREAKING: Caller-managed locking.** The state lock is acquired by the caller through `app.Locks` (`IStateLockCoordinator.Acquire(operation, skipLock, …)`) and held across the operations it guards, rather than each operation taking its own.
- **Schema-read seams on the application.** `app.CurrentSchema` (`ICurrentSchemaProvider`) reads the recorded (offline) or live (online) schema, and `app.PlanFile` (`IPlanFileWriter`) reads and writes saved plan files — exposed as properties alongside `app.Operations` / `app.Locks`.
- **BREAKING: Inspect and release the lock via the coordinator.** `IStateLockCoordinator` now manages the whole lock lifecycle through `app.Locks`: `Peek(ct)` reads the current `StateLockInfo?` without acquiring it, and `Release(ct)` force-releases whatever is held and returns the released lock's details.
- **BREAKING: Operation surface.** Every operation is reached through `app.Operations` (the `INSchemaOperations` facade) with a uniform `XArguments` → `Result<XResult>` shape, and each result carries its outcome as data.
- **BREAKING: Result & diagnostic model.** Operations no longer throw to signal expected outcomes or print their own output. They return `Result`/`Result<T>` carrying success/failure plus `Diagnostic`s, and narrate transient progress through `IProgress<OperationProgress>`; the caller decides what to render.
- **`UseProgressReporter<TProgress>()`.** Registers the `IProgress<OperationProgress>` sink that receives an operation's transient progress narration, replacing the default no-op reporter — a named builder method alongside `UseSqlGenerator`.
- **Atomic file-state writes.** The built-in file state store now writes to a temporary sibling file and atomically renames it into place, so a concurrent reader (e.g. a `plan` reading the recorded state while an `apply` captures new state) never observes a half-written snapshot.
- **Public diff reader.** `DiffReader` is a public, stateless utility (with a shared `.Default` instance) that reads a `DatabaseDiff` into a renderer-neutral `DiffDocument`.

### Changed

- **BREAKING: operations live on `app.Operations`, not `NSchemaApplication`.** `NSchemaApplication` is now a thin facade exposing `Services`, `Operations` (`INSchemaOperations`), `Locks` (`IStateLockCoordinator`), `CurrentSchema` (`ICurrentSchemaProvider`), and `PlanFile` (`IPlanFileWriter`); the per-operation methods (`Plan`, `Apply`, `Refresh`, `Validate`, `Drift`, `Import`, `Doctor`) moved onto `app.Operations`.
- **BREAKING: `IStateLockHandle` is no longer `IAsyncDisposable`.** Release is explicit via `ValueTask Release(CancellationToken)`, and the handle now exposes `StateLockInfo Info` (replacing `string LockId`). Operation call sites release in a `finally`.
- **BREAKING: `IStateLock.ForceUnlock` renamed to `IStateLock.Release`**, now returning `ValueTask` (was `Task<StateLockInfo?>`) to match `IStateLockHandle.Release`. Whether a release is "forced" is decided by the seam the caller reaches for — `IStateLock.Release` removes whatever lock is held; `IStateLockHandle.Release` removes only its own.
- **`StateLockInfo`** gains an optional `ExpiresUtc`.

- **Planning and applying require a state store.** The diff is computed against the current state — the schema *plus* the run-once ledger — so
  planning without a store would plan against knowingly incomplete state, and an apply that cannot record what it ran would silently lose
  history. Both now refuse up front with a clear diagnostic (the plan-time run-once warning is gone — the situation it warned about is
  unrepresentable), and refreshing without a store is a failure result rather than a silent no-op. A teardown now always reads the managed
  schema from the recorded state — the fallback to the declared schema is gone: state is the record of what NSchema manages, and an empty
  record means there is nothing to tear down.

### Added

- **Ephemeral state.** `UseEphemeralState()` declares the target database disposable: it registers an in-memory state store and matching lock
  that live only as long as the application instance — intended for integration tests and container bootstraps, where recorded state has
  nothing to outlive. Run-once scripts behave correctly within the session; nothing persists beyond it.

### Removed

- **BREAKING: `NoOpStateLock` and the no-op lock fallback.** `IStateLock` is now registered only when a state backend supplies one; an operation either takes a real lock or runs without one, rather than acquiring a placeholder. Operations resolve `IStateLock?` (optional).
- **BREAKING: The `ForceUnlock` operation.** `NSchemaApplication.ForceUnlock`, `IForceUnlockOperation`, and `ForceUnlockArguments` are gone; force-unlock is a thin caller of `IStateLock.Release()` (the CLI does the peek + expected-id check + confirmation itself).
- **BREAKING: The `Show` operation.** `NSchemaApplication.Show`, `IShowOperation`, and `ShowArguments` are gone. Reading-and-rendering the recorded state, a saved plan, or (new) the live schema is a thin front-end concern, built on the public read seams above rather than a Core operation.
- **BREAKING: Renderer interfaces and `Use*Renderer` builder methods.** `IDiffRenderer`, `ISchemaRenderer`, and `ISqlPlanRenderer` are gone, along with `UseDiffRenderer<T>()`, `UseTerraformRenderer(…)`, `UseSchemaRenderer<T>()`, and `UseSqlPlanRenderer<T>()`. The renderers were never consumed by Core and had no swap points; they are now public concrete utilities (see Added) rather than DI-registered services. A consumer wanting a different format writes its own renderer and calls it directly.

## [3.4.0] - 2026-06-25

### Added

- **`Doctor` operation.** `NSchemaApplication.Doctor(DoctorArguments)` runs read-only health checks against the configured infrastructure.
- **`IStateLock.Peek`.** Reads the held lock (or `null` when free) without acquiring it, so a diagnostic never contends with a real operation. Added
  as a **default interface method** (returns `null`), so existing implementers are source-compatible.
- **Force-unlock by id.** `ForceUnlockArguments.ExpectedLockId` makes `ForceUnlock` a compare-and-swap: refused with a `StateLockMismatchException`
  unless it matches the held lock. Unset keeps the previous "remove whatever is held" behavior.

## [3.3.0] - 2026-06-24

### Changed

- Dropping a schema now emits specific drop instructions for all known elements beneath. This is required for providers that don't support cascading deletes.

## [3.2.1] - 2026-06-24

### Fixed

- Fixed a bug where trailing comments would get merged when formatting DDL. They should now be preserved.
- Fixed a bug where whitespace between comments and statements would get stripped when formatting DDL. It will now collapse to a single blank line.

## [3.2.0] - 2026-06-21

**More SQL Server Enhancements.** A second gap found while building the SQL Server provider: its triggers carry their action as an inline statement body, not by calling a separate function as PostgreSQL's do.

### Added

- `Trigger` now has an optional `Body` to take an statement body, alongside the existing `Function` that Postgres uses. The two are mutually exclusive: a trigger either executes a function, or runs an inline body (SQL Server). `Body` is optional and defaults to `null`, so the change is source-compatible, and it participates in structural equality (a body change is a drop + recreate, like any other structural trigger change).
- The SQL DDL parser and writer accept and emit the inline form: `CREATE TRIGGER … ON s.t AS $$ … $$` (dollar-quoted, so the body may contain its own `;`), in addition to the existing `… EXECUTE FUNCTION f(args)` form.

## [3.1.0] - 2026-06-21

**SQL Server Enhancements.** In working on the upcoming SQL Server provider, some functionality gaps were identified. This release goes towards enabling the SQL Server provider to work without hacks.

### Added

- When modifying a column's type or nullability, SQL Server requires restating the full column definition. To facilitate this, the `AlterColumnType` and `AlterColumnNullability` actions now include both the desired type and nullability. Both are optional and default to `null`, so the change is source-compatible. A modified column's `ColumnDiff.Definition` is now populated with the desired column, and the plan linearizer threads these final values onto the two actions.

## [3.0.0] - 2026-06-20

**First stable release.** This release is a ground-up rewrite, reworking the `NSchema` library into a thin CLI wrapper around a new `NSchema.Core`.

The full Terraform-style lifecycle (`plan` / `apply` / `destroy`) etc. has been implemented along with features like saved plans, drift detection, backend state and locking.

See the new documentation site for all details: https://nschema.dev.

## [2.1.0] - 2026-06-02

### Added

- Glob support for JSON schemas. `AddJsonSchemasFromGlob("schemas/**/*.json")` registers a provider for every matching file, and `AddJsonSchemasFromDirectory` now matches with the same globbing engine. Each file is aggregated like any other provider.
- `FileSchemaProvider`, a public base class for file-backed `ISchemaProvider`s. It handles file existence, stream lifetime, and schema-name filtering; derived providers implement only the format-specific `Parse`.
- `AddFileSchemasFromGlob` and `AddFileSchemasFromDirectory` builder methods, the shared file-discovery primitives behind the JSON helpers. Both take a provider factory so any file-backed provider can reuse the globbing.

## [2.0.1] - 2026-06-01

### Fixed

- Fixed a bug where trying to register multiple desired schema providers of the same concrete type would only resolve the first one.

### Changed

- `UseStateStoreFile` is now `UseFileStateStore` to align with the other extension methods. The old method still exists, it's just been marked obsolete.
- Schema filtering now uses a dedicated `Filter` method on the `DatabaseSchema` model for better reuse and discoverability.

## [2.0.0] - 2026-06-01

Version 2 focuses on improving developer experience with a more explicit and extensible model for planning and applying changes. It also introduces an optional Terraform-style state store so that plans can be made against snapshots rather than a live database.

### Added

#### Backend state store (new)

By default NSchema plans against the live database, but this isn't always possible. A CI pipeline may have no way to reach the database, or you may want plans to reflect the last deployed state rather than any drift since then.

NSchema now supports an optional state store that persists a snapshot of the schema after every successful apply:

```csharp
builder.UseStateStoreFile("schema_state.json");
```

Once a store is registered, `Plan` operations automatically read from it instead of the live database (offline planning), while `Apply` operations always use the live database. No further configuration is needed.

A new `Refresh` operation captures the current live schema to the store without planning or applying anything. Use this to initialize the store, or to record drift that happened outside of NSchema.

`FileSchemaStateStore` is a ready-made file-backed implementation. Custom stores implement `ISchemaStateStore`. Alongside this release, there will be an `NSchema.Aws` package with an implementation for S3.

#### JSON schemas (new)

Desired schemas can now be declared in a JSON file instead of C#, so you can describe a schema without a compiled project:

```csharp
builder.AddJsonSchema("schema.json");
```

The file mirrors the schema model, with SQL types written as compact strings (`"int"`, `"varchar(255)"`, `"decimal(10,2)"`). Multiple files can be registered and are aggregated like any other provider.

### Upgrading from 1.x

The API has changed significantly. This section is organised around what you need to do, depending on your role.

#### If you are a library user

**The default operation is now `Plan`.** NSchema will not apply changes unless you explicitly configure it with `RunOperation(MigrationOperation.Apply)` or call `app.Apply()`. This prevents accidental data loss when running NSchema for the first time.

- **`DryRun` / `DryRunOnly()` have been removed.** Use `RunOperation(MigrationOperation.Plan)` or `app.Plan()` instead.
- **`NSchemaApplication` now has explicit entry points.** `Plan()`, `Apply()`, and `Refresh()` methods that run a specific operation regardless of the configured default. `RunAsync()` still uses the configured operation.
- **`MigrationOptions` has been broken up.** Settings that control what gets migrated (`SchemaNames`, `DestructiveActionPolicy`) stay in `MigrationOptions`. Settings that control how a run executes (`Operation`, `TransactionMode`) have moved to `MigrationRunOptions` and `SqlExecutorOptions`. The builder methods still work as before; only direct reads of `IOptions<MigrationOptions>` need to change.
- **`PolicyError` has a new `Severity` property.** The existing 2-argument constructor still compiles, but custom `IMigrationPolicy` implementations should use `PolicySeverity.Warning` to signal non-fatal findings rather than returning errors.

#### If you are a database provider

- **`IMigrationReporter` has moved** to the `NSchema.Migration` namespace (was `NSchema.Hosting`). Update any `using` directives.
- **`IMigrationExecutor` and `UseMigrationExecutor<T>()` have been removed.** Implement `IMigrationCompiler` instead. A compiler receives a `MigrationPlan` and returns an executable `ICompiledMigration` unit. Register it with `UseMigrationCompiler<T>()`.
- **`UseCurrentSchema<T>()` is unchanged.** It still registers the live database provider. No action required.
- **`IMigrationPlanner` is now public** and its `Plan()` method now takes explicit `DatabaseSchema currentSchema` and `DatabaseSchema desiredSchema` parameters. If you have a custom planner implementation, update the signature. The planner is now a pure domain service — it no longer resolves schema providers from DI.

## [1.0.1] - 2026-05-28

### Fixed

- Fixed a bug where primary keys, foreign keys, and indexes weren't being displayed in the reported plan for new tables.

## [1.0.0] - 2026-05-27

First stable release. The public API is now covered by semantic versioning. Breaking changes will only ship in a new major version.

### Added

- Declarative schema definition via `AbstractSchemaProvider` and the fluent `Schema` / `Table` / `Column` / `Index` / `ForeignKey` builders.
- Hosted application model: `NSchemaApplication.CreateBuilder(...)` produces an `IHost`-backed app that runs the migration as a `BackgroundService`.
- A single `ISchemaProvider` interface used for both desired-state and current-state schemas, with the role determined at DI registration time (`AddSchema<T>()` / `UseSchemaSource<T>()`).
- Pluggable pipeline with extension points for every stage: `ISchemaPolicy`, `IMigrationPlanTransformer`, `IMigrationPolicy`, `IScriptProvider`, `ISqlPlanner`, `ISqlExecutor`, `IMigrationExecutor`.
- Rename detection for schemas, tables, and columns via `RenamedFrom(...)`.
- Migration options: `DestructiveActionPolicy` (`Error` / `Warn` / `Allow`), `DryRun`, `TransactionMode` (`Single` / `None`) with per-statement `RunOutsideTransaction` carve-outs, and `SchemaNames` scope filter via `ForSchemas(...)`.
- Built-in `ActionOrderingTransformer` (topological ordering of plan actions) and `DestructiveActionMigrationPolicy`.
- Pre- and post-deployment script support via `IScriptProvider`, `AddScriptFromFile(...)`, and `AddScriptsFromEmbeddedResources(...)`.
- SourceLink and symbol packages (`.snupkg`) published alongside the main package for source-level debugging.

[Unreleased]: https://github.com/nschema-org/NSchema.Core/compare/v4.6.1...HEAD
[4.6.1]: https://github.com/nschema-org/NSchema.Core/compare/v4.6.0...v4.6.1
[4.6.0]: https://github.com/nschema-org/NSchema.Core/compare/v4.5.0...v4.6.0
[4.5.0]: https://github.com/nschema-org/NSchema.Core/compare/v4.4.0...v4.5.0
[4.4.0]: https://github.com/nschema-org/NSchema.Core/compare/v4.3.1...v4.4.0
[4.3.1]: https://github.com/nschema-org/NSchema.Core/compare/v4.3.0...v4.3.1
[4.3.0]: https://github.com/nschema-org/NSchema.Core/compare/v4.2.0...v4.3.0
[4.2.0]: https://github.com/nschema-org/NSchema.Core/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/nschema-org/NSchema.Core/compare/v4.0.1...v4.1.0
[4.0.1]: https://github.com/nschema-org/NSchema.Core/compare/v4.0.0...v4.0.1
[4.0.0]: https://github.com/nschema-org/NSchema.Core/compare/v3.1.0...v4.0.0
[3.1.0]: https://github.com/nschema-org/NSchema.Core/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/nschema-org/NSchema.Core/compare/v2.1.0...v3.0.0
[2.1.0]: https://github.com/nschema-org/NSchema.Core/compare/v2.0.1...v2.1.0
[2.0.1]: https://github.com/nschema-org/NSchema.Core/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/nschema-org/NSchema.Core/compare/v1.0.1...v2.0.0
[1.0.1]: https://github.com/nschema-org/NSchema.Core/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/nschema-org/NSchema.Core/releases/tag/v1.0.0
