using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a RENAME directive: <c>RENAME &lt;kind&gt; &lt;current&gt; TO &lt;name&gt;;</c>. The current side is
    /// fully qualified (a bare name for SCHEMA, a three-part path for COLUMN); the target is always a bare
    /// name — a rename never moves an object across containers.
    /// </summary>
    private NsqlStatement ParseRename(Token? doc)
    {
        var rename = Advance(); // RENAME
        var position = rename.Position;

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            var schemaKeyword = Advance();
            var from = ExpectIdentifierNode("a schema name");
            var (to, target, semicolon) = ParseRenameTarget("a schema name");
            return new RenameSchemaStatement(from, target)
            {
                Doc = doc?.Text,
                DocComment = doc,
                RenameKeyword = rename,
                SchemaKeyword = schemaKeyword,
                ToKeyword = to,
                SemicolonToken = semicolon,
            };
        }
        if (_current.IsKeyword(NsqlKeywords.Column))
        {
            var columnKeyword = Advance();
            var from = ParseColumnPath();
            var (to, target, semicolon) = ParseRenameTarget("a column name");
            return new RenameColumnStatement(from, target)
            {
                Doc = doc?.Text,
                DocComment = doc,
                RenameKeyword = rename,
                ColumnKeyword = columnKeyword,
                ToKeyword = to,
                SemicolonToken = semicolon,
            };
        }
        if (TryParseObjectKind() is { } parsed)
        {
            var from = ParseQualifiedNameNode();
            var (to, target, semicolon) = ParseRenameTarget(RenameTargetNoun(parsed.Kind));
            return new RenameObjectStatement(parsed.Kind, from, target)
            {
                Doc = doc?.Text,
                DocComment = doc,
                RenameKeyword = rename,
                KindKeywords = parsed.Keywords,
                ToKeyword = to,
                SemicolonToken = semicolon,
            };
        }

        throw Error("Expected a renameable kind: SCHEMA, TABLE, COLUMN, VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE or ROUTINE.");
    }

    /// <summary>
    /// Consumes an object-kind keyword when the current token names a schema-level object kind. VIEW and
    /// MATERIALIZED VIEW both name a view, and FUNCTION, PROCEDURE and ROUTINE all name a routine (they share
    /// one name space) — the concrete kind is resolved from the current state when the directive is planned.
    /// </summary>
    private (SchemaObjectKind Kind, IReadOnlyList<Token> Keywords)? TryParseObjectKind()
    {
        if (_current.IsKeyword(NsqlKeywords.Table))
        {
            return (SchemaObjectKind.Table, [Advance()]);
        }
        if (_current.IsAnyKeyword(NsqlKeywords.View, NsqlKeywords.Materialized))
        {
            var first = Advance();
            if (first.IsKeyword(NsqlKeywords.Materialized))
            {
                return (SchemaObjectKind.View, [first, ExpectKeyword(NsqlKeywords.View)]);
            }
            return (SchemaObjectKind.View, [first]);
        }
        if (_current.IsKeyword(NsqlKeywords.Enum))
        {
            return (SchemaObjectKind.Enum, [Advance()]);
        }
        if (_current.IsKeyword(NsqlKeywords.Domain))
        {
            return (SchemaObjectKind.Domain, [Advance()]);
        }
        if (_current.IsKeyword(NsqlKeywords.Type))
        {
            return (SchemaObjectKind.CompositeType, [Advance()]);
        }
        if (_current.IsKeyword(NsqlKeywords.Sequence))
        {
            return (SchemaObjectKind.Sequence, [Advance()]);
        }
        if (_current.IsAnyKeyword(NsqlKeywords.Function, NsqlKeywords.Procedure, NsqlKeywords.Routine))
        {
            return (SchemaObjectKind.Routine, [Advance()]);
        }
        return null;
    }

    /// <summary>
    /// What a rename's target should be, for the error when it is missing. The noun follows the keyword the
    /// kind is written as, not the model's name for it (a composite type is written <c>TYPE</c>).
    /// </summary>
    private static string RenameTargetNoun(SchemaObjectKind kind) => kind switch
    {
        SchemaObjectKind.Table => "a table name",
        SchemaObjectKind.View => "a view name",
        SchemaObjectKind.Enum => "an enum name",
        SchemaObjectKind.Domain => "a domain name",
        SchemaObjectKind.CompositeType => "a type name",
        SchemaObjectKind.Sequence => "a sequence name",
        SchemaObjectKind.Routine => "a routine name",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private (Token To, Identifier Name, Token Semicolon) ParseRenameTarget(string what)
    {
        var to = ExpectKeyword(NsqlKeywords.To);
        var name = ExpectIdentifierNode(what);
        var semicolon = Expect(TokenKind.Semicolon, "';'");
        return (to, name, semicolon);
    }

    /// <summary>
    /// Parses a RENAME COLUMN: <c>schema.table.column</c> at the top level, or <c>table.column</c> inside a template.
    /// </summary>
    private MemberPath ParseColumnPath()
    {
        var first = ExpectIdentifierNode("a schema name");
        var firstDot = Expect(TokenKind.Dot, "'.' in the column path");
        var second = ExpectIdentifierNode("a table name");
        if (_inTemplateBody && _current.Kind != TokenKind.Dot)
        {
            // table.column. The schema is decided when the template is applied; the one dot is the member dot.
            return new MemberPath(null, first, second) { MemberDotToken = firstDot };
        }
        var secondDot = Expect(TokenKind.Dot, "'.' in the column path (schema.table.column)");
        var column = ExpectIdentifierNode("a column name");
        return new MemberPath(first, second, column)
        {
            SchemaDotToken = firstDot,
            MemberDotToken = secondDot,
        };
    }
}
