using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.CompositeTypes;
using NSchema.Project.Nsql.Syntax.Constraints;
using NSchema.Project.Nsql.Syntax.Domains;
using NSchema.Project.Nsql.Syntax.Enums;
using NSchema.Project.Nsql.Syntax.Extensions;
using NSchema.Project.Nsql.Syntax.Indexes;
using NSchema.Project.Nsql.Syntax.Routines;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Sequences;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Nsql.Syntax.Triggers;
using NSchema.Project.Nsql.Syntax.Views;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    private NsqlStatement ParseCreate(Token? doc)
    {
        var create = Advance(); // CREATE
        var position = create.Position;

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE SCHEMA is not supported inside a template; apply the template to existing schemas instead.");
            }
            return ParseCreateSchema(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Table))
        {
            return ParseCreateTable(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.View))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE VIEW is not supported inside a template: a view body is opaque, so its references cannot be re-pointed at each target schema.");
            }
            var view = Advance(); // VIEW
            return ParseCreateView(create, materializedKeyword: null, view, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Materialized))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE MATERIALIZED VIEW is not supported inside a template: a view body is opaque, so its references cannot be re-pointed at each target schema.");
            }
            var materialized = Advance(); // MATERIALIZED
            var view = ExpectKeyword(NsqlKeywords.View);
            return ParseCreateView(create, materialized, view, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Enum))
        {
            return ParseCreateEnum(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Domain))
        {
            return ParseCreateDomain(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Type))
        {
            return ParseCreateCompositeType(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Sequence))
        {
            return ParseCreateSequence(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Function))
        {
            return ParseCreateRoutine(create, doc, RoutineKind.Function);
        }
        if (_current.IsKeyword(NsqlKeywords.Procedure))
        {
            return ParseCreateRoutine(create, doc, RoutineKind.Procedure);
        }
        if (_current.IsKeyword(NsqlKeywords.Aggregate))
        {
            return ParseCreateRoutine(create, doc, RoutineKind.Aggregate);
        }
        if (_current.IsKeyword(NsqlKeywords.Extension))
        {
            if (_inTemplateBody)
            {
                throw Error("CREATE EXTENSION is not supported inside a template; extensions are database-global.");
            }
            return ParseCreateExtension(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Trigger))
        {
            return ParseCreateTrigger(create, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Index))
        {
            return ParseCreateIndex(create, uniqueKeyword: null, doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Unique))
        {
            var unique = Advance(); // UNIQUE
            return ParseCreateIndex(create, unique, doc);
        }
        throw Error($"Expected SCHEMA, TABLE, VIEW, MATERIALIZED VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE, AGGREGATE, EXTENSION, TRIGGER or INDEX after CREATE, found '{_current.Text}'.");
    }

    private CreateSchemaStatement ParseCreateSchema(Token create, Token? doc)
    {
        var schema = Advance(); // SCHEMA
        var name = ExpectIdentifierNode("a schema name");
        var semicolon = Expect(TokenKind.Semicolon, "';'");
        return new CreateSchemaStatement(name)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            SchemaKeyword = schema,
            SemicolonToken = semicolon,
        };
    }

    private CreateTableStatement ParseCreateTable(Token create, Token? doc)
    {
        var tableKeyword = Advance(); // TABLE
        var name = ParseQualifiedNameNode();

        var open = Expect(TokenKind.LeftParen, "'(' to begin the table body");
        var members = new List<TableMember>();
        var separators = new List<Token>();
        var primaryKeySeen = false;
        do
        {
            var itemDoc = TakePendingDoc();
            members.Add(ParseTableItem(itemDoc, ref primaryKeySeen));
        }
        while (TryConsumeSeparator(TokenKind.Comma, separators));
        var close = Expect(TokenKind.RightParen, "')' or ',' after a table member");
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateTableStatement(name, new SeparatedSyntaxList<TableMember>(members, separators))
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            TableKeyword = tableKeyword,
            OpenParenToken = open,
            CloseParenToken = close,
            SemicolonToken = semicolon,
        };
    }

    // The "VIEW" keyword has already been consumed by the dispatcher (preceded by "MATERIALIZED" when the view
    // is materialized); both are threaded in so the statement reprints exactly.
    private CreateViewStatement ParseCreateView(Token create, Token? materializedKeyword, Token view, Token? doc)
    {
        var name = ParseQualifiedNameNode();
        var asKeyword = ExpectKeyword(NsqlKeywords.As);

        // The body is captured verbatim; projection derives the view's dependencies from it.
        var (body, bodyToken) = CaptureRawSpanToken("a view body", [TokenKind.Semicolon]);
        var semicolon = Expect(TokenKind.Semicolon, "';' to end the view definition");

        return new CreateViewStatement(name, body, materializedKeyword is not null)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            MaterializedKeyword = materializedKeyword,
            ViewKeyword = view,
            AsKeyword = asKeyword,
            BodyToken = bodyToken,
            SemicolonToken = semicolon,
        };
    }

    /// <summary>
    /// Parses a standalone index: <c>CREATE [UNIQUE] INDEX name ON s.relation (cols) [WHERE (expr)]</c>. The
    /// "UNIQUE" keyword (if present) has already been consumed by the dispatcher.
    /// </summary>
    private CreateIndexStatement ParseCreateIndex(Token create, Token? uniqueKeyword, Token? doc)
    {
        var indexKeyword = ExpectKeyword(NsqlKeywords.Index);
        var name = ExpectIdentifierNode("an index name");
        var onKeyword = ExpectKeyword(NsqlKeywords.On);
        var on = ParseQualifiedNameNode();
        var method = TryParseIndexMethod();
        var (open, keys, close) = ParseIndexColumns();
        var include = TryParseInclude();
        var where = TryParseWhereClause();
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateIndexStatement(name, uniqueKeyword is not null, on, keys, method?.Method, include?.Columns, where?.Predicate)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            UniqueKeyword = uniqueKeyword,
            IndexKeyword = indexKeyword,
            OnKeyword = onKeyword,
            UsingKeyword = method?.Using,
            OpenParenToken = open,
            CloseParenToken = close,
            IncludeKeyword = include?.Include,
            WhereKeyword = where?.Where,
            WhereOpenParenToken = where?.Open,
            PredicateToken = where?.Span,
            WhereCloseParenToken = where?.Close,
            SemicolonToken = semicolon,
        };
    }

    private CreateRoutineStatement ParseCreateRoutine(Token create, Token? doc, RoutineKind kind)
    {
        var kindKeyword = Advance(); // FUNCTION | PROCEDURE
        var what = kind == RoutineKind.Procedure ? "a procedure definition" : "a function definition";
        var name = ParseQualifiedNameNode();
        var (open, arguments, argumentsSpan, close) = CaptureParenthesizedToken();
        var (definition, definitionSpan) = CaptureRawSpanToken(what, [TokenKind.Semicolon]);
        var semicolon = Expect(TokenKind.Semicolon, $"';' to end {what}");

        return new CreateRoutineStatement(name, kind, arguments, definition)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            KindKeyword = kindKeyword,
            OpenParenToken = open,
            ArgumentsToken = argumentsSpan,
            CloseParenToken = close,
            DefinitionToken = definitionSpan,
            SemicolonToken = semicolon,
        };
    }

    private CreateEnumStatement ParseCreateEnum(Token create, Token? doc)
    {
        var enumKeyword = Advance(); // ENUM
        var name = ParseQualifiedNameNode();

        var open = Expect(TokenKind.LeftParen, "'(' to begin the enum values");
        var values = new List<EnumValue>();
        var separators = new List<Token>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var valueToken = _current;
                Expect(TokenKind.String, "an enum value (a quoted string)");
                if (values.Any(v => string.Equals(v.Value, valueToken.Text, StringComparison.Ordinal)))
                {
                    throw new NsqlSyntaxException($"Enum value '{valueToken.Text}' is declared more than once.", valueToken.Position);
                }
                values.Add(new EnumValue(valueToken));
            }
            while (TryConsumeSeparator(TokenKind.Comma, separators));
        }
        var close = Expect(TokenKind.RightParen, "')' or ',' after an enum value");
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateEnumStatement(name, new SeparatedSyntaxList<EnumValue>(values, separators))
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            EnumKeyword = enumKeyword,
            OpenParenToken = open,
            CloseParenToken = close,
            SemicolonToken = semicolon,
        };
    }

    /// <summary>
    /// Parses a domain: <c>CREATE DOMAIN s.d AS &lt;type&gt; [NOT NULL | NULL] [CONSTRAINT n CHECK (e)]… [DEFAULT expr]</c>.
    /// The optional <c>DEFAULT</c> clause, if present, must come last: its expression is opaque and read up to the
    /// terminating <c>;</c>.
    /// </summary>
    private CreateDomainStatement ParseCreateDomain(Token create, Token? doc)
    {
        var domainKeyword = Advance(); // DOMAIN
        var name = ParseQualifiedNameNode();
        var asKeyword = ExpectKeyword(NsqlKeywords.As);
        var dataType = ParseTypeNode();
        var tailStart = _current;

        SqlText? @default = null;
        var notNull = false;
        var checks = new List<CheckDefinition>();

        while (@default is null && _current.Kind != TokenKind.Semicolon)
        {
            if (_current.IsKeyword(NsqlKeywords.Not))
            {
                Advance();
                ExpectKeyword(NsqlKeywords.Null);
                notNull = true;
            }
            else if (_current.IsKeyword(NsqlKeywords.Null))
            {
                Advance();
                notNull = false;
            }
            else if (_current.IsKeyword(NsqlKeywords.Constraint))
            {
                var checkPosition = _current.Position;
                Advance();
                var checkName = ExpectIdentifierNode("a constraint name");
                ExpectKeyword(NsqlKeywords.Check);
                checks.Add(new CheckDefinition(checkName, ReadRawExpression(parenthesised: true)));
            }
            else if (_current.IsKeyword(NsqlKeywords.Default))
            {
                Advance();
                // The default is opaque and read to the terminating ';', so it must be the final clause.
                @default = CaptureRawSpan("a domain default", [TokenKind.Semicolon]);
            }
            else
            {
                throw Error($"Expected NOT NULL, NULL, CONSTRAINT … CHECK, DEFAULT or ';', found '{_current.Text}'.");
            }
        }

        var semicolon = Expect(TokenKind.Semicolon, "';'");
        var tail = tailStart.Position.Offset < semicolon.Position.Offset ? RawSpanFrom(tailStart, semicolon) : (Token?)null;

        return new CreateDomainStatement(name, dataType, notNull, checks, @default)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            DomainKeyword = domainKeyword,
            AsKeyword = asKeyword,
            TailToken = tail,
            SemicolonToken = semicolon,
        };
    }

    /// <summary>
    /// Parses a composite type: <c>CREATE TYPE s.t AS (field &lt;type&gt;, field &lt;type&gt;, …)</c>.
    /// </summary>
    private CreateCompositeTypeStatement ParseCreateCompositeType(Token create, Token? doc)
    {
        var typeKeyword = Advance(); // TYPE
        var name = ParseQualifiedNameNode();
        var asKeyword = ExpectKeyword(NsqlKeywords.As);
        var open = Expect(TokenKind.LeftParen, "'(' to begin the composite type fields");

        var fields = new List<CompositeFieldDefinition>();
        var separators = new List<Token>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var fieldName = ExpectIdentifierNode("a field name");
                var fieldType = ParseTypeNode();
                fields.Add(new CompositeFieldDefinition(fieldName, fieldType));
            }
            while (TryConsumeSeparator(TokenKind.Comma, separators));
        }

        var close = Expect(TokenKind.RightParen, "')' or ',' after a composite type field");
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateCompositeTypeStatement(name, new SeparatedSyntaxList<CompositeFieldDefinition>(fields, separators))
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            TypeKeyword = typeKeyword,
            AsKeyword = asKeyword,
            OpenParenToken = open,
            CloseParenToken = close,
            SemicolonToken = semicolon,
        };
    }

    private CreateSequenceStatement ParseCreateSequence(Token create, Token? doc)
    {
        var sequenceKeyword = Advance(); // SEQUENCE
        var name = ParseQualifiedNameNode();
        var options = TryParseSequenceOptions();
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateSequenceStatement(name, options)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            SequenceKeyword = sequenceKeyword,
            SemicolonToken = semicolon,
        };
    }

    private SequenceOptionsClause? TryParseSequenceOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return null;
        }
        var open = Advance();
        var clausePosition = open.Position;

        TypeName? dataType = null;
        long? start = null, increment = null, min = null, max = null, cache = null;
        var cycle = false;
        do
        {
            var optionPosition = _current.Position;
            var option = ExpectIdentifierNode("a sequence option");

            void RejectDuplicate(bool alreadySet)
            {
                if (alreadySet)
                {
                    throw new NsqlSyntaxException($"Sequence option '{option.Value.ToUpperInvariant()}' is specified more than once.", optionPosition);
                }
            }

            if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.As))
            {
                RejectDuplicate(dataType is not null);
                var typeName = ExpectIdentifierNode("a type name");
                dataType = new TypeName(null, typeName);
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Start))
            {
                RejectDuplicate(start is not null);
                start = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Increment))
            {
                RejectDuplicate(increment is not null);
                increment = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.MinValue))
            {
                RejectDuplicate(min is not null);
                min = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.MaxValue))
            {
                RejectDuplicate(max is not null);
                max = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Cache))
            {
                RejectDuplicate(cache is not null);
                cache = ExpectSignedIntegerValue();
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Cycle))
            {
                RejectDuplicate(cycle);
                cycle = true;
            }
            else
            {
                throw new NsqlSyntaxException(
                    $"Unknown sequence option '{option.Value}'; expected {NsqlKeywords.As}, {NsqlKeywords.Start}, {NsqlKeywords.Increment}, {NsqlKeywords.MinValue}, {NsqlKeywords.MaxValue}, {NsqlKeywords.Cache} or {NsqlKeywords.Cycle}.", optionPosition);
            }
        }
        while (Match(TokenKind.Comma));
        var close = Expect(TokenKind.RightParen, "')'");

        return new SequenceOptionsClause(dataType, start, increment, min, max, cache, cycle)
        {
            OpenParenToken = open,
            InteriorToken = RawSpanBetween(open, close),
            CloseParenToken = close,
        };
    }

    private CreateExtensionStatement ParseCreateExtension(Token create, Token? doc)
    {
        var extension = Advance(); // EXTENSION
        var name = ParseExtensionNameNode();
        string? version = null;
        Token? versionKeyword = null;
        Token? versionToken = null;
        if (_current.IsKeyword(NsqlKeywords.Version))
        {
            versionKeyword = Advance();
            var value = Expect(TokenKind.String, "a version string after VERSION");
            versionToken = value;
            version = value.Text;
        }
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateExtensionStatement(name, version)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            ExtensionKeyword = extension,
            VersionKeyword = versionKeyword,
            VersionToken = versionToken,
            SemicolonToken = semicolon,
        };
    }

    /// <summary>
    /// Reads an extension name, which may be a bare identifier (<c>citext</c>) or a quoted string
    /// (<c>'uuid-ossp'</c>) — extension names commonly contain characters, such as a hyphen, that a bare
    /// identifier cannot.
    /// </summary>
    private Identifier ParseExtensionNameNode()
    {
        if (_current.Kind == TokenKind.String)
        {
            var token = Advance();
            return new Identifier(token);
        }
        return ExpectIdentifierNode("an extension name");
    }

    /// <summary>
    /// Parses a standalone trigger: <c>CREATE TRIGGER name {BEFORE|AFTER|INSTEAD OF} event {OR event} ON s.t
    /// [FOR EACH {ROW|STATEMENT}] [WHEN (expr)] {EXECUTE {FUNCTION|PROCEDURE} f(args) | AS $$ body $$}</c>.
    /// </summary>
    private CreateTriggerStatement ParseCreateTrigger(Token create, Token? doc)
    {
        var triggerKeyword = Advance(); // TRIGGER
        var name = ExpectIdentifierNode("a trigger name");

        // The header (timing, events, ON table, FOR EACH, WHEN) is keyword-heavy and order-fixed; it reprints as a
        // verbatim span while the parsed fields carry the semantics. It runs to the action anchor (EXECUTE or AS).
        var headerStart = _current;
        var timing = ParseTriggerTiming();
        var (events, updateOfColumns) = ParseTriggerEvents();

        ExpectKeyword(NsqlKeywords.On);
        var on = ParseQualifiedNameNode();

        var level = TriggerLevel.Statement;
        if (_current.IsKeyword(NsqlKeywords.For))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Each);
            if (_current.IsKeyword(NsqlKeywords.Row)) { Advance(); level = TriggerLevel.Row; }
            else if (_current.IsKeyword(NsqlKeywords.Statement)) { Advance(); level = TriggerLevel.Statement; }
            else { throw Error($"Expected ROW or STATEMENT after FOR EACH, found '{_current.Text}'."); }
        }

        SqlText? when = null;
        if (_current.IsKeyword(NsqlKeywords.When))
        {
            Advance();
            when = ReadRawExpression(parenthesised: true);
        }

        var header = RawSpanFrom(headerStart, _current);

        TriggerAction action;
        var actionPosition = _current.Position;
        if (_current.IsKeyword(NsqlKeywords.Execute))
        {
            var actionStart = _current;
            Advance();
            if (_current.IsAnyKeyword(NsqlKeywords.Function, NsqlKeywords.Procedure))
            {
                Advance();
            }
            else
            {
                throw Error($"Expected FUNCTION or PROCEDURE after EXECUTE, found '{_current.Text}'.");
            }

            var first = ExpectIdentifierNode("a function name");
            var function = Match(TokenKind.Dot)
                ? new QualifiedName(first, ExpectIdentifierNode("a function name"))
                : new QualifiedName(null, first);

            // The argument list is captured verbatim (opaque), like a routine's; usually empty for a trigger function.
            var arguments = CaptureParenthesized();
            action = new ExecuteFunctionAction(function, arguments)
            {
                ActionToken = RawSpanFrom(actionStart, _current),
            };
        }
        else if (_current.IsKeyword(NsqlKeywords.As))
        {
            // An inline body is a dollar-quoted block (so it may contain its own ';'), like a deployment script.
            var asKeyword = Advance();
            var dollar = Expect(TokenKind.DollarString, "a trigger body as a dollar-quoted block ($$ … $$)");
            action = new InlineBodyAction(StripDollarQuote(dollar.Text).Trim())
            {
                AsKeyword = asKeyword,
                BodyToken = dollar,
            };
        }
        else
        {
            throw Error($"Expected EXECUTE or AS to begin the trigger action, found '{_current.Text}'.");
        }

        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new CreateTriggerStatement(name, timing, events, on, action, updateOfColumns, level, when)
        {
            Doc = doc?.Text,
            DocComment = doc,
            CreateKeyword = create,
            TriggerKeyword = triggerKeyword,
            HeaderToken = header,
            SemicolonToken = semicolon,
        };
    }

    private TriggerTiming ParseTriggerTiming()
    {
        if (_current.IsKeyword(NsqlKeywords.Before)) { Advance(); return TriggerTiming.Before; }
        if (_current.IsKeyword(NsqlKeywords.After)) { Advance(); return TriggerTiming.After; }
        if (_current.IsKeyword(NsqlKeywords.Instead)) { Advance(); ExpectKeyword(NsqlKeywords.Of); return TriggerTiming.InsteadOf; }
        throw Error($"Expected BEFORE, AFTER or INSTEAD OF, found '{_current.Text}'.");
    }

    private (TriggerEvent Events, List<Identifier>? UpdateOfColumns) ParseTriggerEvents()
    {
        var events = TriggerEvent.None;
        List<Identifier>? updateOfColumns = null;
        while (true)
        {
            var position = _current.Position;
            if (_current.IsKeyword(NsqlKeywords.Insert))
            {
                Advance();
                AddEvent(TriggerEvent.Insert, position);
            }
            else if (_current.IsKeyword(NsqlKeywords.Delete))
            {
                Advance();
                AddEvent(TriggerEvent.Delete, position);
            }
            else if (_current.IsKeyword(NsqlKeywords.Truncate))
            {
                Advance();
                AddEvent(TriggerEvent.Truncate, position);
            }
            else if (_current.IsKeyword(NsqlKeywords.Update))
            {
                Advance();
                AddEvent(TriggerEvent.Update, position);
                if (_current.IsKeyword(NsqlKeywords.Of))
                {
                    Advance();
                    updateOfColumns = [.. ParseColumnList()];
                }
            }
            else
            {
                throw Error($"Expected INSERT, UPDATE, DELETE or TRUNCATE, found '{_current.Text}'.");
            }

            if (!_current.IsKeyword(NsqlKeywords.Or))
            {
                return (events, updateOfColumns);
            }
            Advance(); // OR
        }

        void AddEvent(TriggerEvent next, SourcePosition position)
        {
            if (events.HasFlag(next))
            {
                throw new NsqlSyntaxException($"Trigger event '{next.ToString().ToUpperInvariant()}' is specified more than once.", position);
            }
            events |= next;
        }
    }

    private TableMember ParseTableItem(Token? doc, ref bool primaryKeySeen)
    {
        var member = ParseTableItemCore(doc);
        if (member is PrimaryKeyDefinition)
        {
            if (primaryKeySeen)
            {
                throw Error("A table may declare only one primary key.");
            }
            primaryKeySeen = true;
        }
        return member;
    }

    private TableMember ParseTableItemCore(Token? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.Constraint))
        {
            return ParseConstraint(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Unique))
        {
            return ParseIndexMember(doc, isUnique: true);
        }
        if (_current.IsKeyword(NsqlKeywords.Index))
        {
            return ParseIndexMember(doc, isUnique: false);
        }
        if (_current.IsKeyword(NsqlKeywords.Include))
        {
            return ParseIncludeOrColumn(doc);
        }
        if (_current.IsName)
        {
            return ParseColumn(doc);
        }
        throw Error("Expected a column or constraint definition.");
    }

    /// <summary>
    /// Disambiguates a member starting with <c>INCLUDE</c>: it is a template include only when nothing follows
    /// the name (the member ends at <c>,</c> or <c>)</c>); otherwise it is a column named <c>include</c> whose
    /// type is the identifier — the pre-template reading, kept so existing DDL parses unchanged.
    /// </summary>
    private TableMember ParseIncludeOrColumn(Token? doc)
    {
        var include = Advance(); // INCLUDE (or a column named 'include')

        if (_current.IsName
            && _lexer.Peek().Kind is TokenKind.Comma or TokenKind.RightParen)
        {
            if (_inTableTemplateBody)
            {
                throw Error("INCLUDE is not supported inside a table template; a table template cannot include another.");
            }
            var templateName = ExpectIdentifierNode("a template name");
            return new IncludeMember(templateName)
            {
                Doc = doc?.Text,
                DocComment = doc,
                IncludeKeyword = include,
            };
        }

        return ParseColumn(new Identifier(include), doc);
    }

    private TableMember ParseColumn(Token? doc)
        => ParseColumn(ExpectIdentifierNode("a column name"), doc);

    private TableMember ParseColumn(Identifier name, Token? doc)
    {
        var type = ParseTypeNode();
        var modifiersStart = _current;

        var isNullable = true;
        if (_current.IsKeyword(NsqlKeywords.Not))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Null);
            isNullable = false;
        }
        else if (_current.IsKeyword(NsqlKeywords.Null))
        {
            Advance();
        }

        var isIdentity = false;
        IdentityOptionsClause? identity = null;
        if (_current.IsKeyword(NsqlKeywords.Identity))
        {
            Advance();
            isIdentity = true;
            identity = TryParseIdentityOptions();
        }

        SqlText? defaultExpression = null;
        if (_current.IsKeyword(NsqlKeywords.Default))
        {
            Advance();
            defaultExpression = ReadRawExpression(parenthesised: false);
        }

        SqlText? generatedExpression = null;
        if (_current.IsKeyword(NsqlKeywords.Generated))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Always);
            ExpectKeyword(NsqlKeywords.As);
            generatedExpression = ReadRawExpression(parenthesised: true);
            ExpectKeyword(NsqlKeywords.Stored);
        }

        // The modifiers after the type are order-fixed and opaque enough to reprint verbatim; the parsed fields
        // above carry the semantics. The span runs from the first modifier to the member terminator (',' or ')').
        var modifiers = modifiersStart.Position.Offset < _current.Position.Offset ? RawSpanFrom(modifiersStart, _current) : (Token?)null;

        return new ColumnDefinition(name, type, isNullable, isIdentity, identity, defaultExpression, generatedExpression)
        {
            Doc = doc?.Text,
            DocComment = doc,
            ModifiersToken = modifiers,
        };
    }

    private TypeName ParseTypeNode()
    {
        var first = ExpectIdentifierNode("a column type");
        Identifier? schema = null;
        var name = first;
        Token? schemaDot = null;
        if (_current.Kind == TokenKind.Dot)
        {
            // A schema-qualified user-defined type, e.g. an enum referenced as `app.status`.
            schemaDot = Advance();
            schema = first;
            name = ExpectIdentifierNode("a schema-qualified type name");
        }
        string? arguments = null;
        Token? open = null, precision = null, comma = null, scale = null, close = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            open = Advance();
            precision = Expect(TokenKind.Integer, "an integer");
            arguments = precision.Value.Text;
            if (_current.Kind == TokenKind.Comma)
            {
                comma = Advance();
                scale = Expect(TokenKind.Integer, "an integer");
                arguments += NsqlSymbols.Comma + scale.Value.Text;
            }
            close = Expect(TokenKind.RightParen, "')'");
        }
        return new TypeName(schema, name, arguments)
        {
            SchemaDotToken = schemaDot,
            OpenParenToken = open,
            PrecisionToken = precision,
            CommaToken = comma,
            ScaleToken = scale,
            CloseParenToken = close,
        };
    }

    private IdentityOptionsClause? TryParseIdentityOptions()
    {
        if (_current.Kind != TokenKind.LeftParen)
        {
            return null;
        }
        var clausePosition = _current.Position;
        Advance();

        long? start = null, min = null, increment = null;
        do
        {
            var option = ExpectIdentifierNode($"{NsqlKeywords.Start}, {NsqlKeywords.Increment} or {NsqlKeywords.MinValue}");
            var value = ExpectIntegerValue();
            if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Start))
            {
                start = value;
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.Increment))
            {
                increment = value;
            }
            else if (NsqlKeywords.Comparer.Equals(option.Value, NsqlKeywords.MinValue))
            {
                min = value;
            }
            else
            {
                throw Error($"Unknown identity option '{option.Value}'; expected {NsqlKeywords.Start}, {NsqlKeywords.Increment} or {NsqlKeywords.MinValue}.");
            }
        }
        while (Match(TokenKind.Comma));
        Expect(TokenKind.RightParen, "')'");

        return new IdentityOptionsClause(start, increment, min);
    }

    private TableMember ParseConstraint(Token? doc)
    {
        var constraint = Advance(); // CONSTRAINT
        var position = constraint.Position;
        var name = ExpectIdentifierNode("a constraint name");

        if (_current.IsKeyword(NsqlKeywords.Primary))
        {
            var primary = Advance();
            var key = ExpectKeyword(NsqlKeywords.Key);
            var columns = ParseColumnList();
            return new PrimaryKeyDefinition(name, columns)
            {
                Doc = doc?.Text,
                DocComment = doc,
                ConstraintKeyword = constraint,
                PrimaryKeyword = primary,
                KeyKeyword = key,
            };
        }
        if (_current.IsKeyword(NsqlKeywords.Foreign))
        {
            var foreign = Advance();
            var key = ExpectKeyword(NsqlKeywords.Key);
            var columns = ParseColumnList();
            var references = ExpectKeyword(NsqlKeywords.References);
            var referencedTable = ParseQualifiedNameNode();
            var refColumns = ParseColumnList();
            var actionsStart = _current;
            var (onDelete, onUpdate) = ParseReferentialActions();
            var actions = actionsStart.Position.Offset < _current.Position.Offset ? RawSpanFrom(actionsStart, _current) : (Token?)null;
            return new ForeignKeyDefinition(name, columns, referencedTable, refColumns, onDelete, onUpdate)
            {
                Doc = doc?.Text,
                DocComment = doc,
                ConstraintKeyword = constraint,
                ForeignKeyword = foreign,
                KeyKeyword = key,
                ReferencesKeyword = references,
                ActionsToken = actions,
            };
        }
        if (_current.IsKeyword(NsqlKeywords.Unique))
        {
            var unique = Advance();
            var columns = ParseColumnList();
            return new UniqueDefinition(name, columns)
            {
                Doc = doc?.Text,
                DocComment = doc,
                ConstraintKeyword = constraint,
                UniqueKeyword = unique,
            };
        }
        if (_current.IsKeyword(NsqlKeywords.Check))
        {
            var check = Advance();
            var (open, expression, span, close) = CaptureParenthesizedToken();
            return new CheckDefinition(name, expression)
            {
                Doc = doc?.Text,
                DocComment = doc,
                ConstraintKeyword = constraint,
                CheckKeyword = check,
                OpenParenToken = open,
                ExpressionToken = span,
                CloseParenToken = close,
            };
        }
        if (_current.IsKeyword(NsqlKeywords.Exclude))
        {
            var exclude = Advance();
            return ParseExclusion(constraint, exclude, name, doc);
        }
        throw Error($"Expected PRIMARY KEY, FOREIGN KEY, UNIQUE, CHECK or EXCLUDE, found '{_current.Text}'.");
    }

    private (ReferentialAction OnDelete, ReferentialAction OnUpdate) ParseReferentialActions()
    {
        var onDelete = ReferentialAction.NoAction;
        var onUpdate = ReferentialAction.NoAction;
        while (_current.IsKeyword(NsqlKeywords.On))
        {
            Advance();
            if (_current.IsKeyword(NsqlKeywords.Delete))
            {
                Advance();
                onDelete = ParseReferentialAction();
            }
            else if (_current.IsKeyword(NsqlKeywords.Update))
            {
                Advance();
                onUpdate = ParseReferentialAction();
            }
            else
            {
                throw Error($"Expected DELETE or UPDATE after ON, found '{_current.Text}'.");
            }
        }
        return (onDelete, onUpdate);
    }

    private ReferentialAction ParseReferentialAction()
    {
        if (_current.IsKeyword(NsqlKeywords.No))
        {
            Advance();
            ExpectKeyword(NsqlKeywords.Action);
            return ReferentialAction.NoAction;
        }
        if (_current.IsKeyword(NsqlKeywords.Cascade))
        {
            Advance();
            return ReferentialAction.Cascade;
        }
        if (_current.IsKeyword(NsqlKeywords.Set))
        {
            Advance();
            if (_current.IsKeyword(NsqlKeywords.Null))
            {
                Advance();
                return ReferentialAction.SetNull;
            }
            if (_current.IsKeyword(NsqlKeywords.Default))
            {
                Advance();
                return ReferentialAction.SetDefault;
            }
            throw Error($"Expected NULL or DEFAULT after SET, found '{_current.Text}'.");
        }
        throw Error($"Expected a referential action (NO ACTION, CASCADE, SET NULL, SET DEFAULT), found '{_current.Text}'.");
    }

    /// <summary>
    /// Parses an exclusion constraint body (the <c>EXCLUDE</c> keyword is already consumed):
    /// <c>[USING method] ( element WITH operator [, …] ) [WHERE (predicate)]</c>.
    /// </summary>
    private ExclusionDefinition ParseExclusion(Token constraint, Token exclude, Identifier name, Token? doc)
    {
        var method = TryParseIndexMethod();
        var open = Expect(TokenKind.LeftParen, "'(' to begin the exclusion elements");
        var elements = new List<ExclusionElement> { ParseExclusionElement() };
        var separators = new List<Token>();
        while (TryConsumeSeparator(TokenKind.Comma, separators))
        {
            elements.Add(ParseExclusionElement());
        }
        var close = Expect(TokenKind.RightParen, "')' or ',' after an exclusion element");

        var where = TryParseWhereClause();

        return new ExclusionDefinition(name, new SeparatedSyntaxList<ExclusionElement>(elements, separators), method?.Method, where?.Predicate)
        {
            Doc = doc?.Text,
            DocComment = doc,
            ConstraintKeyword = constraint,
            ExcludeKeyword = exclude,
            UsingKeyword = method?.Using,
            OpenParenToken = open,
            CloseParenToken = close,
            WhereKeyword = where?.Where,
            WhereOpenParenToken = where?.Open,
            PredicateToken = where?.Span,
            WhereCloseParenToken = where?.Close,
        };
    }

    private ExclusionElement ParseExclusionElement()
    {
        var position = _current.Position;
        Identifier? column = null;
        SqlText? expression = null;
        Token? exprOpen = null, exprSpan = null, exprClose = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            var (open, inner, span, close) = CaptureParenthesizedToken();
            expression = new SqlText(inner);
            exprOpen = open;
            exprSpan = span;
            exprClose = close;
        }
        else
        {
            column = ExpectIdentifierNode("a column name or expression");
        }

        if (!_current.IsKeyword(NsqlKeywords.With))
        {
            throw Error($"Expected WITH after an exclusion element, found '{_current.Text}'.");
        }

        // The operator is opaque (=, &&, <>, …) and uses characters lexed as Symbol tokens, so "WITH <operator>" is
        // captured verbatim from the WITH keyword up to the element separator or list close, then the leading WITH is
        // stripped for the semantic value while the span reprints it whole.
        var (raw, withSpan) = CaptureRawSpanToken("an exclusion operator", [TokenKind.Comma, TokenKind.RightParen]);
        var @operator = raw.TrimStart()[NsqlKeywords.With.Length..].Trim();

        return new ExclusionElement(@operator, column, expression)
        {
            OpenParenToken = exprOpen,
            ExpressionToken = exprSpan,
            CloseParenToken = exprClose,
            WithOperatorToken = withSpan,
        };
    }

    private IndexDefinition ParseIndexMember(Token? doc, bool isUnique)
    {
        var position = _current.Position;
        Token? uniqueKeyword = null;
        if (isUnique)
        {
            uniqueKeyword = Advance(); // UNIQUE
        }
        var indexKeyword = ExpectKeyword(NsqlKeywords.Index);
        var name = ExpectIdentifierNode("an index name");
        var method = TryParseIndexMethod();
        var (open, keys, close) = ParseIndexColumns();
        var include = TryParseInclude();
        var where = TryParseWhereClause();

        return new IndexDefinition(name, isUnique, keys, method?.Method, include?.Columns, where?.Predicate)
        {
            Doc = doc?.Text,
            DocComment = doc,
            UniqueKeyword = uniqueKeyword,
            IndexKeyword = indexKeyword,
            UsingKeyword = method?.Using,
            OpenParenToken = open,
            CloseParenToken = close,
            IncludeKeyword = include?.Include,
            WhereKeyword = where?.Where,
            WhereOpenParenToken = where?.Open,
            PredicateToken = where?.Span,
            WhereCloseParenToken = where?.Close,
        };
    }

    /// <summary>Parses the optional <c>USING &lt;method&gt;</c> clause of an index; returns <see langword="null"/> when absent (default B-tree).</summary>
    private (Token Using, Identifier Method)? TryParseIndexMethod()
    {
        if (!_current.IsKeyword(NsqlKeywords.Using))
        {
            return null;
        }
        var usingKeyword = Advance();
        return (usingKeyword, ExpectIdentifierNode("an index access method"));
    }

    /// <summary>Parses the optional covering <c>INCLUDE (cols)</c> clause of an index; returns <see langword="null"/> when absent.</summary>
    private (Token Include, ColumnList Columns)? TryParseInclude()
    {
        if (!_current.IsKeyword(NsqlKeywords.Include))
        {
            return null;
        }
        var include = Advance();
        return (include, ParseColumnList());
    }

    /// <summary>Parses an optional <c>WHERE (predicate)</c> clause; returns <see langword="null"/> when absent.</summary>
    private (Token Where, Token Open, SqlText Predicate, Token Span, Token Close)? TryParseWhereClause()
    {
        if (!_current.IsKeyword(NsqlKeywords.Where))
        {
            return null;
        }
        var where = Advance();
        var (open, inner, span, close) = CaptureParenthesizedToken();
        return (where, open, new SqlText(inner), span, close);
    }

    /// <summary>
    /// Parses an index key list: each key is a column name or a parenthesised expression, with an optional
    /// <c>ASC</c>/<c>DESC</c> and <c>NULLS FIRST</c>/<c>NULLS LAST</c>.
    /// </summary>
    private (Token Open, SeparatedSyntaxList<IndexElement> Keys, Token Close) ParseIndexColumns()
    {
        var open = Expect(TokenKind.LeftParen, "'('");
        var keys = new List<IndexElement> { ParseIndexKey() };
        var separators = new List<Token>();
        while (TryConsumeSeparator(TokenKind.Comma, separators))
        {
            keys.Add(ParseIndexKey());
        }
        var close = Expect(TokenKind.RightParen, "')'");
        return (open, new SeparatedSyntaxList<IndexElement>(keys, separators), close);
    }

    private IndexElement ParseIndexKey()
    {
        var position = _current.Position;
        Identifier? column = null;
        SqlText? expression = null;
        Token? exprOpen = null, exprSpan = null, exprClose = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            // A parenthesised expression key, e.g. (lower(email)).
            var (open, inner, span, close) = CaptureParenthesizedToken();
            expression = new SqlText(inner);
            exprOpen = open;
            exprSpan = span;
            exprClose = close;
        }
        else
        {
            column = ExpectIdentifierNode("a column name or expression");
        }

        var sort = IndexSort.Default;
        Token? sortToken = null;
        if (_current.IsKeyword(NsqlKeywords.Asc))
        {
            sortToken = Advance();
            sort = IndexSort.Ascending;
        }
        else if (_current.IsKeyword(NsqlKeywords.Desc))
        {
            sortToken = Advance();
            sort = IndexSort.Descending;
        }

        var nulls = IndexNulls.Default;
        Token? nullsKeyword = null, nullsPosition = null;
        if (_current.IsKeyword(NsqlKeywords.Nulls))
        {
            nullsKeyword = Advance();
            if (_current.IsKeyword(NsqlKeywords.First))
            {
                nullsPosition = Advance();
                nulls = IndexNulls.First;
            }
            else if (_current.IsKeyword(NsqlKeywords.Last))
            {
                nullsPosition = Advance();
                nulls = IndexNulls.Last;
            }
            else
            {
                throw Error($"Expected FIRST or LAST after NULLS, found '{_current.Text}'.");
            }
        }

        return new IndexElement(column, expression, sort, nulls)
        {
            OpenParenToken = exprOpen,
            ExpressionToken = exprSpan,
            CloseParenToken = exprClose,
            SortToken = sortToken,
            NullsKeyword = nullsKeyword,
            NullsPositionToken = nullsPosition,
        };
    }

    private ColumnList ParseColumnList()
    {
        var open = Expect(TokenKind.LeftParen, "'('");
        var columns = new List<Identifier> { ExpectIdentifierNode("a column name") };
        var separators = new List<Token>();
        while (TryConsumeSeparator(TokenKind.Comma, separators))
        {
            columns.Add(ExpectIdentifierNode("a column name"));
        }
        var close = Expect(TokenKind.RightParen, "')'");
        return new ColumnList(new SeparatedSyntaxList<Identifier>(columns, separators))
        {
            OpenParenToken = open,
            CloseParenToken = close,
        };
    }

    /// <summary>
    /// Captures an opaque SQL expression as raw text: a balanced <c>( … )</c> when <paramref name="parenthesised"/>
    /// (CHECK / WHERE), or an unparenthesised DEFAULT value otherwise (terminated by the enclosing column list's
    /// <c>,</c> / <c>)</c>).
    /// </summary>
    private SqlText ReadRawExpression(bool parenthesised) => new(
        parenthesised
            ? CaptureParenthesized()
            : CaptureRawSpan("a default expression", [TokenKind.Comma, TokenKind.RightParen]));

}
