using System.Globalization;
using System.Text;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Services;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Routines;
using NSchema.Project.Nsql.Tokens;
using Syn = NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// Builds syntax documents from the domain model.
/// </summary>
internal static class SyntaxBuilder
{
    private static readonly SourcePosition _none = SourcePosition.None;

    public static NsqlDocument Build(Database database, bool declareSchemas = true) =>
        Build(database, ProjectDirectives.Empty, declareSchemas);

    public static NsqlDocument Build(Database database, ProjectDirectives directives, bool declareSchemas = true)
    {
        var statements = new List<NsqlStatement>();

        // Extensions are database-global and are created first, so they precede the schemas.
        foreach (var extension in database.Extensions)
        {
            statements.Add(Build(extension));
        }

        foreach (var definition in database.Schemas)
        {
            AddSchema(statements, definition, declareSchemas);
        }

        // Scripts are directives; each kind renders straight from its home on the directives.
        foreach (var script in directives.ChangeScripts)
        {
            statements.Add(Build(script));
        }
        foreach (var script in directives.DeploymentScripts)
        {
            statements.Add(Build(script));
        }

        AddDirectives(statements, directives);

        return new NsqlDocument(statements);
    }

    /// <summary>
    /// Emits the directive statements after the declarations: the renames, container level first.
    /// </summary>
    private static void AddDirectives(List<NsqlStatement> statements, ProjectDirectives directives)
    {
        foreach (var rename in directives.SchemaRenames)
        {
            statements.Add(new Syn.Schemas.RenameSchemaStatement(Name(rename.From.Name), Name(rename.To.Name)));
        }
        foreach (var rename in directives.ObjectRenames)
        {
            // An object rename always names the kind it renames; one without is not a rename we can write.
            if (rename.From.Kind is not { } kind)
            {
                continue;
            }

            statements.Add(new RenameObjectStatement(kind, Qualified(rename.From.Schema, rename.From.Name), Name(rename.To))
            {
                KindKeywords = [Token.Keyword(KindKeyword(kind))],
            });
        }
        foreach (var rename in directives.MemberRenames)
        {
            statements.Add(new Syn.Tables.RenameColumnStatement(
                new MemberPath(Name(rename.From.Schema), Name(rename.From.Object), Name(rename.From.Member)),
                Name(rename.To))
           );
        }


    }

    private static void AddSchema(List<NsqlStatement> statements, Schema schema, bool declare)
    {
        if (declare && !schema.IsImplicit)
        {
            statements.Add(new Syn.Schemas.CreateSchemaStatement(Name(schema.Name))
            {
                Doc = schema.Comment,
                DocComment = DocToken(schema.Comment),
            });

            foreach (var grant in schema.Grants)
            {
                statements.Add(new Syn.Schemas.GrantSchemaUsageStatement(Name(schema.Name), Name(grant.Role)));
            }
        }

        foreach (var enumType in schema.Enums)
        {
            statements.Add(new Syn.Enums.CreateEnumStatement(Qualified(schema.Name, enumType.Name),
                new SeparatedSyntaxList<Syn.Enums.EnumValue>(enumType.Values.Select(v => Syn.Enums.EnumValue.Synthetic(v.Value)).ToList()))
            {
                Doc = enumType.Comment,
                DocComment = DocToken(enumType.Comment),
            });
        }

        foreach (var domain in schema.Domains)
        {
            var node = new Syn.Domains.CreateDomainStatement(Qualified(schema.Name, domain.Name), Type(domain.DataType), domain.NotNull,
                domain.Checks.Select(c => new Syn.Constraints.CheckDefinition(Name(c.Name), c.Expression)).ToList(),
                Default(domain.Default))
            {
                Doc = domain.Comment,
                DocComment = DocToken(domain.Comment),
            };
            statements.Add(node with { TailToken = DomainTail(node) is { Length: > 0 } tail ? Token.Span(tail) : null });
        }

        foreach (var compositeType in schema.CompositeTypes)
        {
            statements.Add(new Syn.CompositeTypes.CreateCompositeTypeStatement(Qualified(schema.Name, compositeType.Name),
                new SeparatedSyntaxList<Syn.CompositeTypes.CompositeFieldDefinition>(
                    compositeType.Fields.Select(f => new Syn.CompositeTypes.CompositeFieldDefinition(Name(f.Name), Type(f.DataType))).ToList()))
            {
                Doc = compositeType.Comment,
                DocComment = DocToken(compositeType.Comment),
            });
        }

        foreach (var sequence in schema.Sequences)
        {
            statements.Add(new Syn.Sequences.CreateSequenceStatement(Qualified(schema.Name, sequence.Name),
                Options(sequence.Options))
            {
                Doc = sequence.Comment,
                DocComment = DocToken(sequence.Comment),
            });
        }

        foreach (var routine in schema.Routines)
        {
            statements.Add(new CreateRoutineStatement(Qualified(schema.Name, routine.Name),
                routine.RoutineKind.ToSyntax(), routine.Arguments, routine.Definition)
            {
                Doc = routine.Comment,
                DocComment = DocToken(routine.Comment),
                ArgumentsToken = ArgumentsSpan(routine.Arguments),
                DefinitionToken = OpaqueSpan(routine.Definition),
            });
        }

        // Collections precede the tables whose columns are typed by them, so a read encounters each in order.
        foreach (var collection in schema.XmlSchemaCollections)
        {
            statements.Add(new Syn.XmlSchemaCollections.CreateXmlSchemaCollectionStatement(
                Qualified(schema.Name, collection.Name), collection.Body)
            {
                Doc = collection.Comment,
                DocComment = DocToken(collection.Comment),
                BodyToken = OpaqueSpan(collection.Body),
            });
        }

        foreach (var table in schema.Tables)
        {
            AddTable(statements, schema.Name, table);
        }

        foreach (var view in schema.Views)
        {
            AddView(statements, schema.Name, view);
        }


    }

    private static void AddTable(List<NsqlStatement> statements, SqlIdentifier schemaName, Table table)
    {
        var members = new List<Syn.Tables.TableMember>();

        foreach (var column in table.Columns)
        {
            var node = new Syn.Tables.ColumnDefinition(Name(column.Name), Type(column.Type), column.IsNullable, column.IsIdentity,
                Options(column.IdentityOptions), Default(column.DefaultExpression), column.GeneratedExpression, column.IsStored, column.IsRowGuid, column.DefaultConstraintName is { } defaultName ? Name(defaultName) : null)
            {
                Doc = column.Comment,
                DocComment = DocToken(column.Comment),
            };
            members.Add(node with { ModifiersToken = ColumnModifiers(node) is { Length: > 0 } modifiers ? Token.Span(modifiers) : null });
        }
        if (table.PrimaryKey is { } pk)
        {
            members.Add(new Syn.Constraints.PrimaryKeyDefinition(Name(pk.Name), ColumnList(pk.ColumnNames), pk.Clustered) { Doc = pk.Comment, DocComment = DocToken(pk.Comment) });
        }
        foreach (var fk in table.ForeignKeys)
        {
            var node = new Syn.Constraints.ForeignKeyDefinition(Name(fk.Name), ColumnList(fk.ColumnNames),
                Qualified(fk.References.Schema, fk.References.Name), ColumnList(fk.ReferencedColumnNames),
                Action(fk.OnDelete), Action(fk.OnUpdate))
            {
                Doc = fk.Comment,
                DocComment = DocToken(fk.Comment),
            };
            members.Add(node with { ActionsToken = ForeignKeyActions(node) is { Length: > 0 } actions ? Token.Span(actions) : null });
        }
        foreach (var unique in table.UniqueConstraints)
        {
            members.Add(new Syn.Constraints.UniqueDefinition(Name(unique.Name), ColumnList(unique.ColumnNames), unique.Clustered) { Doc = unique.Comment, DocComment = DocToken(unique.Comment) });
        }
        foreach (var check in table.CheckConstraints)
        {
            members.Add(new Syn.Constraints.CheckDefinition(Name(check.Name), check.Expression) { Doc = check.Comment, DocComment = DocToken(check.Comment) });
        }
        foreach (var exclusion in table.ExclusionConstraints)
        {
            members.Add(new Syn.Constraints.ExclusionDefinition(Name(exclusion.Name),
                new SeparatedSyntaxList<Syn.Constraints.ExclusionElement>(
                    exclusion.Elements.Select(e => new Syn.Constraints.ExclusionElement(e.Operator, OptionalName(e.Column), e.Expression)).ToList()),
                OptionalName(exclusion.Method), exclusion.Predicate)
            {
                Doc = exclusion.Comment,
                DocComment = DocToken(exclusion.Comment),
            });
        }
        foreach (var index in table.Indexes.Where(i => i.Xml is null))
        {
            members.Add(new Syn.Indexes.IndexDefinition(Name(index.Name), index.IsUnique, Keys(index.Columns),
                OptionalName(index.Method),
                IncludeList(index.Include), index.Predicate, index.Clustered)
            {
                Doc = index.Comment,
                DocComment = DocToken(index.Comment),
            });
        }

        statements.Add(new Syn.Tables.CreateTableStatement(Qualified(schemaName, table.Name), new SeparatedSyntaxList<Syn.Tables.TableMember>(members))
        {
            Doc = table.Comment,
            DocComment = DocToken(table.Comment),
        });

        // An XML index is standalone: a secondary names the primary it is built over, and a table member has no
        // way to refer to another index. Emitted after the table, primaries first, so a read sees each in order.
        foreach (var index in table.Indexes.Where(i => i.Xml is not null).OrderBy(i => i.Xml!.IsPrimary ? 0 : 1))
        {
            statements.Add(IndexStatement(schemaName, table.Name, index));
        }

        foreach (var grant in table.Grants)
        {
            statements.Add(new Syn.Tables.GrantTableStatement(Privileges(grant.Privileges), Qualified(schemaName, table.Name), Name(grant.Role))
            {
            });
        }

        // Triggers are standalone statements (like grants), emitted after their table so the table exists when
        // they are read back.
        foreach (var trigger in table.Triggers)
        {
            statements.Add(Build(schemaName, table.Name, trigger));
        }
    }

    private static void AddView(List<NsqlStatement> statements, SqlIdentifier schemaName, View view)
    {
        statements.Add(new Syn.Views.CreateViewStatement(Qualified(schemaName, view.Name), view.Body, view.IsMaterialized, view.IsSchemaBound)
        {
            Doc = view.Comment,
            DocComment = DocToken(view.Comment),
            BodyToken = OpaqueSpan(view.Body),
        });

        // A view's indexes are standalone statements emitted after it.
        foreach (var index in view.Indexes)
        {
            statements.Add(IndexStatement(schemaName, view.Name, index));
        }
    }

    private static Syn.Triggers.CreateTriggerStatement Build(SqlIdentifier schemaName, SqlIdentifier tableName, Trigger trigger)
    {
        Syn.Triggers.TriggerAction action;
        if (trigger.Body is { } body)
        {
            action = new Syn.Triggers.InlineBodyAction(body) { BodyToken = DollarString(body) };
        }
        else if (trigger.Function is { } function)
        {
            var fn = new Syn.Triggers.ExecuteFunctionAction(
                new QualifiedName(OptionalName(function.Schema), Name(function.Name)),
                trigger.FunctionArguments ?? "");
            action = fn with { ActionToken = Token.Span(ExecuteFunctionText(fn)) };
        }
        else
        {
            throw new InvalidOperationException($"Trigger '{trigger.Name}' has neither a body nor a function to execute.");
        }

        var node = new Syn.Triggers.CreateTriggerStatement(Name(trigger.Name), Timing(trigger.Timing), Events(trigger.Events),
            Qualified(schemaName, tableName), action,
            trigger.UpdateOfColumns is { Count: > 0 } updateOf ? Names(updateOf) : null,
            trigger.Level == TriggerLevel.Row ? Syn.Triggers.TriggerLevel.Row : Syn.Triggers.TriggerLevel.Statement,
            trigger.When, trigger.IsNotForReplication)
        {
            Doc = trigger.Comment,
            DocComment = DocToken(trigger.Comment),
        };
        return node with { HeaderToken = Token.Span(TriggerHeader(node)) };
    }

    private static Syn.Scripts.ScriptStatement Build(Script script)
    {
        Syn.Scripts.ScriptEventClause clause = script switch
        {
            DeploymentScript deployment => new Syn.Scripts.DeploymentEventClause(deployment.Phase == DeploymentPhase.Pre ? Syn.Scripts.DeploymentPhase.Pre : Syn.Scripts.DeploymentPhase.Post)
            {
            },
            ChangeScript change => new Syn.Scripts.ChangeEventClause(Trigger(change.Target.Trigger), new MemberPath(OptionalName(change.Target.Schema), Name(change.Target.Table), Name(change.Target.Member)))
            {
            },
            _ => throw new InvalidOperationException($"Unbuildable script '{script.GetType().Name}'."),
        };

        // A bare RUN is the default (ALWAYS); only ONCE is written. A change-event script always runs bare.
        Syn.Scripts.RunCondition? condition = script is DeploymentScript { RunCondition: RunCondition.Once }
            ? Syn.Scripts.RunCondition.Once
            : null;

        return new Syn.Scripts.ScriptStatement(Name(script.Name), condition, clause, script.Sql, script.RunOutsideTransaction)
        {
            BodyToken = DollarString(script.Sql),
        };
    }

    private static Syn.Extensions.CreateExtensionStatement Build(Extension extension) =>
        new(Name(extension.Name), extension.Version)
        {
            Doc = extension.Comment,
            DocComment = DocToken(extension.Comment),
            VersionToken = extension.Version is { } version ? Token.StringLiteral(version) : null,
        };

    // --- synthetic tokens -------------------------------------------------------------

    /// <summary>A synthetic doc-comment token for a comment body, or null when there is none.</summary>
    private static Token? DocToken(string? comment) => comment is null ? null : new Token(TokenKind.DocComment, comment, _none);

    /// <summary>A synthetic dollar-quoted string token wrapping <paramref name="body"/>.</summary>
    private static Token DollarString(SqlText body) => new(TokenKind.DollarString, body.Value, _none) { Raw = DollarBlock(body) };

    /// <summary>
    /// An opaque body as a token: dollar-quoted when the bare text would not re-parse (a top-level <c>;</c>
    /// ends the statement early, and a line comment on the final line swallows the terminator), verbatim otherwise.
    /// </summary>
    private static Token OpaqueSpan(SqlText body) =>
        SqlLexer.HasTopLevelSemicolon(body.Value) || SqlLexer.EndsInLineComment(body.Value)
            ? DollarString(body)
            : Token.Span(body.Value.TrimEnd());

    /// <summary>
    /// An argument list as a token: a line comment on its final line would swallow the closing <c>)</c>, so such a
    /// list gains a trailing newline (the parenthesised capture trims it back out).
    /// </summary>
    private static Token ArgumentsSpan(SqlText arguments) =>
        SqlLexer.EndsInLineComment(arguments.Value) ? Token.Span($"{arguments.Value}\n") : Token.Span(arguments.Value);

    private static Identifier Name(SqlIdentifier name) => Identifier.Synthetic(name.Value);

    private static Identifier? OptionalName(SqlIdentifier? name) => name is null ? null : Name(name);

    private static List<Identifier> Names(IReadOnlyList<SqlIdentifier> names) => names.Select(Name).ToList();

    private static ColumnList ColumnList(IReadOnlyList<SqlIdentifier> names) => Syn.ColumnList.Synthetic(Names(names));

    private static QualifiedName Qualified(SqlIdentifier schema, SqlIdentifier name) =>
        new(Name(schema), Name(name));

    /// <summary>
    /// Decomposes a type's canonical text (<c>varchar(100)</c>, <c>app.status</c>) into the written form;
    /// the renderer reassembles the same text, so the round-trip is exact.
    /// </summary>
    private static SqlText? Default(SqlDefaultExpression? @default) => @default != null ? new SqlText(@default.Value) : null;

    private static TypeName Type(SqlType type)
    {
        // The type carries its qualifier and arguments as components, so read them straight across — no
        // rendering to a string and splitting it back apart.
        var schema = type.Schema is { } qualifier ? Identifier.Synthetic(qualifier.Value) : null;

        // A typed xml's argument names an object, not a number, so it is written as a qualified name.
        if (type.Xml is { } xml)
        {
            return new TypeName(schema, Identifier.Synthetic(type.Name.Value), null,
                Qualified(xml.Collection.Schema, xml.Collection.Name), xml.IsDocument);
        }

        var arguments = Arguments(type);
        return new TypeName(schema, Identifier.Synthetic(type.Name.Value), arguments);
    }

    private static string? Arguments(SqlType type)
    {
        if (type.Precision is { } precision)
        {
            return $"{precision},{type.Scale}";
        }

        return type.Length is { } length ? length.ToString() : null;
    }

    private static Syn.Tables.IdentityOptionsClause? Options(IdentityOptions? options) =>
        options is null ? null : new Syn.Tables.IdentityOptionsClause(options.StartWith, options.IncrementBy, options.MinValue, options.NotForReplication);

    private static Syn.Sequences.SequenceOptionsClause? Options(SequenceOptions options)
    {
        if (options.DataType is null && options.StartWith is null && options.IncrementBy is null
            && options.MinValue is null && options.MaxValue is null && options.Cache is null && !options.Cycle)
        {
            return null;
        }
        var clause = new Syn.Sequences.SequenceOptionsClause(options.DataType is { } type ? Type(type) : null,
            options.StartWith, options.IncrementBy, options.MinValue, options.MaxValue, options.Cache, options.Cycle);
        // The clause exists only when there are options, so the interior text is always present.
        return clause with { InteriorToken = Token.Span(SequenceOptionsText(clause) ?? "") };
    }

    /// <summary>One standalone <c>CREATE INDEX</c> on <paramref name="relation"/>.</summary>
    private static Syn.Indexes.CreateIndexStatement IndexStatement(SqlIdentifier schemaName, SqlIdentifier relation, TableIndex index) =>
        new(Name(index.Name), index.IsUnique, Qualified(schemaName, relation),
            Keys(index.Columns), OptionalName(index.Method),
            IncludeList(index.Include), index.Predicate,
            index.Xml is { } xml ? new Syn.Indexes.XmlIndexClause(xml.Kind, OptionalName(xml.PrimaryIndex)) : null,
            index.Clustered)
        {
            Doc = index.Comment,
            DocComment = DocToken(index.Comment),
        };

    private static SeparatedSyntaxList<Syn.Indexes.IndexElement> Keys(IReadOnlyList<IndexColumn> columns) =>
        new(columns.Select(c => new Syn.Indexes.IndexElement(OptionalName(c.Column), c.Expression, Sort(c.Sort), Nulls(c.Nulls))).ToList());

    private static ColumnList? IncludeList(IReadOnlyList<SqlIdentifier> names) => names.Count == 0 ? null : ColumnList(names);

    private static Syn.Constraints.ReferentialAction Action(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => Syn.Constraints.ReferentialAction.Cascade,
        ReferentialAction.Restrict => Syn.Constraints.ReferentialAction.Restrict,
        ReferentialAction.SetNull => Syn.Constraints.ReferentialAction.SetNull,
        ReferentialAction.SetDefault => Syn.Constraints.ReferentialAction.SetDefault,
        _ => Syn.Constraints.ReferentialAction.NoAction,
    };

    private static Syn.Indexes.IndexSort Sort(IndexSort sort) => sort switch
    {
        IndexSort.Ascending => Syn.Indexes.IndexSort.Ascending,
        IndexSort.Descending => Syn.Indexes.IndexSort.Descending,
        _ => Syn.Indexes.IndexSort.Default,
    };

    private static Syn.Indexes.IndexNulls Nulls(IndexNulls nulls) => nulls switch
    {
        IndexNulls.First => Syn.Indexes.IndexNulls.First,
        IndexNulls.Last => Syn.Indexes.IndexNulls.Last,
        _ => Syn.Indexes.IndexNulls.Default,
    };

    private static Syn.Triggers.TriggerTiming Timing(TriggerTiming timing) => timing switch
    {
        TriggerTiming.Before => Syn.Triggers.TriggerTiming.Before,
        TriggerTiming.After => Syn.Triggers.TriggerTiming.After,
        _ => Syn.Triggers.TriggerTiming.InsteadOf,
    };

    private static Syn.Triggers.TriggerEvent Events(TriggerEvent events)
    {
        var mapped = Syn.Triggers.TriggerEvent.None;
        if (events.HasFlag(TriggerEvent.Insert)) { mapped |= Syn.Triggers.TriggerEvent.Insert; }
        if (events.HasFlag(TriggerEvent.Update)) { mapped |= Syn.Triggers.TriggerEvent.Update; }
        if (events.HasFlag(TriggerEvent.Delete)) { mapped |= Syn.Triggers.TriggerEvent.Delete; }
        if (events.HasFlag(TriggerEvent.Truncate)) { mapped |= Syn.Triggers.TriggerEvent.Truncate; }
        return mapped;
    }

    private static SeparatedSyntaxList<Syn.Tables.Privilege> Privileges(TablePrivilege privileges)
    {
        var mapped = new List<Syn.Tables.Privilege>();
        if (privileges.HasFlag(TablePrivilege.Select)) { mapped.Add(Syn.Tables.Privilege.Synthetic(Syn.Tables.TablePrivilege.Select)); }
        if (privileges.HasFlag(TablePrivilege.Insert)) { mapped.Add(Syn.Tables.Privilege.Synthetic(Syn.Tables.TablePrivilege.Insert)); }
        if (privileges.HasFlag(TablePrivilege.Update)) { mapped.Add(Syn.Tables.Privilege.Synthetic(Syn.Tables.TablePrivilege.Update)); }
        if (privileges.HasFlag(TablePrivilege.Delete)) { mapped.Add(Syn.Tables.Privilege.Synthetic(Syn.Tables.TablePrivilege.Delete)); }
        return new SeparatedSyntaxList<Syn.Tables.Privilege>(mapped);
    }

    private static Syn.Scripts.ChangeTrigger Trigger(ChangeTrigger trigger) => trigger switch
    {
        ChangeTrigger.AddColumn => Syn.Scripts.ChangeTrigger.AddColumn,
        ChangeTrigger.AlterColumnType => Syn.Scripts.ChangeTrigger.AlterColumnType,
        _ => Syn.Scripts.ChangeTrigger.AddConstraint,
    };

    // --- raw-span fragment rendering (the verbatim text baked into raw-span tokens) -----------

    /// <summary>The trigger header between the name and the action: timing, events, <c>ON</c> table, and (for a function trigger) <c>FOR EACH</c>/<c>WHEN</c>.</summary>
    private static string TriggerHeader(Syn.Triggers.CreateTriggerStatement statement)
    {
        var sb = new StringBuilder();
        sb.Append(TimingText(statement.Timing)).Append(' ').Append(EventsText(statement)).Append($" {NsqlKeywords.On} ").Append(Qualified(statement.On));
        if (statement.Action is Syn.Triggers.ExecuteFunctionAction)
        {
            sb.Append($" {NsqlKeywords.For} {NsqlKeywords.Each} ").Append(statement.Level == Syn.Triggers.TriggerLevel.Row ? NsqlKeywords.Row : NsqlKeywords.Statement);
            if (statement.When is { } when)
            {
                sb.Append($" {NsqlKeywords.When} (").Append(when.Value).Append(')');
            }
        }
        if (statement.NotForReplication)
        {
            sb.Append($" {NsqlKeywords.Not} {NsqlKeywords.For} {NsqlKeywords.Replication}");
        }
        return sb.ToString();
    }

    /// <summary>The <c>EXECUTE FUNCTION function(args)</c> action of a function trigger.</summary>
    private static string ExecuteFunctionText(Syn.Triggers.ExecuteFunctionAction action) =>
        $"{NsqlKeywords.Execute} {NsqlKeywords.Function} {Qualified(action.Function)}({action.Arguments.Value})";

    /// <summary>A dollar-quoted block for <paramref name="body"/>, tag chosen so the body is taken verbatim.</summary>
    private static string DollarBlock(SqlText body)
    {
        var delimiter = DollarDelimiter(body);
        return $"{delimiter}\n{body.Value}\n{delimiter}";
    }

    /// <summary>The column modifiers after the type (<c>NOT NULL IDENTITY DEFAULT GENERATED</c>), or "" when none.</summary>
    private static string ColumnModifiers(Syn.Tables.ColumnDefinition column)
    {
        var parts = new List<string>();
        if (column.RowGuid)
        {
            parts.Add(NsqlKeywords.RowGuidCol);
        }
        if (!column.IsNullable)
        {
            parts.Add($"{NsqlKeywords.Not} {NsqlKeywords.Null}");
        }
        if (column.IsIdentity)
        {
            parts.Add(column.IdentityOptions is { } options && IdentityOptionsText(options) is { } text
                ? $"{NsqlKeywords.Identity} ({text})"
                : NsqlKeywords.Identity);
            if (column.IdentityOptions is { NotForReplication: true })
            {
                parts.Add($"{NsqlKeywords.Not} {NsqlKeywords.For} {NsqlKeywords.Replication}");
            }
        }
        if (column.DefaultConstraintName is { } constraintName)
        {
            parts.Add($"{NsqlKeywords.Constraint} {constraintName.Value}");
        }
        if (column.Default is { } @default)
        {
            parts.Add($"{NsqlKeywords.Default} {@default.Value}");
        }
        if (column.Generated is { } generated)
        {
            var storage = column.Stored ? NsqlKeywords.Stored : NsqlKeywords.Virtual;
            parts.Add($"{NsqlKeywords.Generated} {NsqlKeywords.Always} {NsqlKeywords.As} ({generated.Value}) {storage}");
        }
        return string.Join(" ", parts);
    }

    /// <summary>The foreign-key <c>ON DELETE</c>/<c>ON UPDATE</c> actions, or "" when both are the default.</summary>
    private static string ForeignKeyActions(Syn.Constraints.ForeignKeyDefinition fk)
    {
        var parts = new List<string>();
        if (fk.OnDelete != Syn.Constraints.ReferentialAction.NoAction)
        {
            parts.Add($"{NsqlKeywords.On} {NsqlKeywords.Delete} {ActionText(fk.OnDelete)}");
        }
        if (fk.OnUpdate != Syn.Constraints.ReferentialAction.NoAction)
        {
            parts.Add($"{NsqlKeywords.On} {NsqlKeywords.Update} {ActionText(fk.OnUpdate)}");
        }
        return string.Join(" ", parts);
    }

    /// <summary>The domain clauses after the type (<c>NOT NULL</c>, checks, <c>DEFAULT</c>), or "" when none.</summary>
    private static string DomainTail(Syn.Domains.CreateDomainStatement domain)
    {
        var parts = new List<string>();
        if (domain.NotNull)
        {
            parts.Add($"{NsqlKeywords.Not} {NsqlKeywords.Null}");
        }
        foreach (var check in domain.Checks)
        {
            parts.Add($"{NsqlKeywords.Constraint} {check.Name.Value} {NsqlKeywords.Check} ({check.Expression.Value})");
        }
        if (domain.Default is { } @default)
        {
            parts.Add($"{NsqlKeywords.Default} {@default.Value}");
        }
        return string.Join(" ", parts);
    }

    private static string? SequenceOptionsText(Syn.Sequences.SequenceOptionsClause options)
    {
        var parts = new List<string>();
        if (options.As is { } type)
        {
            parts.Add($"{NsqlKeywords.As} {TypeText(type)}");
        }
        if (options.Start is { } start)
        {
            parts.Add($"{NsqlKeywords.Start} {start}");
        }
        if (options.Increment is { } increment)
        {
            parts.Add($"{NsqlKeywords.Increment} {increment}");
        }
        if (options.MinValue is { } min)
        {
            parts.Add($"{NsqlKeywords.MinValue} {min}");
        }
        if (options.MaxValue is { } max)
        {
            parts.Add($"{NsqlKeywords.MaxValue} {max}");
        }
        if (options.Cache is { } cache)
        {
            parts.Add($"{NsqlKeywords.Cache} {cache}");
        }
        if (options.Cycle)
        {
            parts.Add(NsqlKeywords.Cycle);
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? IdentityOptionsText(Syn.Tables.IdentityOptionsClause options)
    {
        var parts = new List<string>();
        if (options.Start is { } start)
        {
            parts.Add($"{NsqlKeywords.Start} {start}");
        }
        if (options.Increment is { } increment)
        {
            parts.Add($"{NsqlKeywords.Increment} {increment}");
        }
        if (options.MinValue is { } min)
        {
            parts.Add($"{NsqlKeywords.MinValue} {min}");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string TimingText(Syn.Triggers.TriggerTiming timing) => timing switch
    {
        Syn.Triggers.TriggerTiming.Before => NsqlKeywords.Before,
        Syn.Triggers.TriggerTiming.After => NsqlKeywords.After,
        _ => $"{NsqlKeywords.Instead} {NsqlKeywords.Of}",
    };

    private static string EventsText(Syn.Triggers.CreateTriggerStatement statement)
    {
        var parts = new List<string>(4);
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Insert))
        {
            parts.Add(NsqlKeywords.Insert);
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Update))
        {
            parts.Add(statement.UpdateOfColumns is { Count: > 0 } columns ? $"{NsqlKeywords.Update} {NsqlKeywords.Of} ({ColumnsText(columns)})" : NsqlKeywords.Update);
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Delete))
        {
            parts.Add(NsqlKeywords.Delete);
        }
        if (statement.Events.HasFlag(Syn.Triggers.TriggerEvent.Truncate))
        {
            parts.Add(NsqlKeywords.Truncate);
        }
        return string.Join($" {NsqlKeywords.Or} ", parts);
    }

    private static string ActionText(Syn.Constraints.ReferentialAction action) => action switch
    {
        Syn.Constraints.ReferentialAction.Cascade => NsqlKeywords.Cascade,
        Syn.Constraints.ReferentialAction.Restrict => NsqlKeywords.Restrict,
        Syn.Constraints.ReferentialAction.SetNull => $"{NsqlKeywords.Set} {NsqlKeywords.Null}",
        Syn.Constraints.ReferentialAction.SetDefault => $"{NsqlKeywords.Set} {NsqlKeywords.Default}",
        _ => $"{NsqlKeywords.No} {NsqlKeywords.Action}",
    };

    /// <summary>
    /// The canonical keyword for an object kind; the spelling variants (MATERIALIZED VIEW, FUNCTION,
    /// PROCEDURE) normalize to it.
    /// </summary>
    private static string KindKeyword(SchemaObjectKind kind) => kind switch
    {
        SchemaObjectKind.Table => NsqlKeywords.Table,
        SchemaObjectKind.View => NsqlKeywords.View,
        SchemaObjectKind.Enum => NsqlKeywords.Enum,
        SchemaObjectKind.Sequence => NsqlKeywords.Sequence,
        SchemaObjectKind.Routine => NsqlKeywords.Routine,
        SchemaObjectKind.Domain => NsqlKeywords.Domain,
        SchemaObjectKind.CompositeType => NsqlKeywords.Type,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string Qualified(QualifiedName name) =>
        name.Schema is { } schema ? $"{EscapedIdentifier(schema)}.{EscapedIdentifier(name.Name)}" : EscapedIdentifier(name.Name);

    private static string TypeText(TypeName type)
    {
        var text = type.Schema is { } schema ? $"{EscapedIdentifier(schema)}.{EscapedIdentifier(type.Name)}" : EscapedIdentifier(type.Name);
        return type.Arguments is { } arguments ? $"{text}({arguments})" : text;
    }

    private static string ColumnsText(IReadOnlyList<Identifier> columns) => string.Join(", ", columns.Select(EscapedIdentifier));

    private static string DollarDelimiter(SqlText body)
    {
        if (!body.Value.Contains("$$", StringComparison.Ordinal))
        {
            return "$$";
        }
        for (var i = 1; ; i++)
        {
            var tag = $"$body{i.ToString(CultureInfo.InvariantCulture)}$";
            if (!body.Value.Contains(tag, StringComparison.Ordinal))
            {
                return tag;
            }
        }
    }

    private static string EscapedIdentifier(Identifier identifier) => Identifier.NeedsQuoting(identifier.Value)
        ? $"\"{identifier.Value.Replace("\"", "\"\"")}\""
        : identifier.Value;
}
