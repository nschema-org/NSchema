using NSchema.Model;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a template definition.
    /// </summary>
    private NsqlStatement ParseTemplate(Token? doc)
    {
        var template = Advance(); // TEMPLATE
        var name = ExpectIdentifierNode("a template name");
        var (forKeyword, kindKeyword, forTable) = ParseTemplateKind();
        var begin = ExpectKeyword(NsqlKeywords.Begin);

        var header = new TemplateHeader(template, forKeyword, kindKeyword, begin, doc);
        return forTable
            ? ParseTableTemplateBody(header, name)
            : ParseSchemaTemplateBody(header, name);
    }

    /// <summary>The tokens of a template's header — <c>TEMPLATE [FOR SCHEMA|TABLE] BEGIN</c> — and its doc-comment.</summary>
    private readonly record struct TemplateHeader(Token Template, Token? For, Token? Kind, Token Begin, Token? Doc);

    /// <summary>
    /// Parses the optional kind marker after the template name: <c>FOR SCHEMA</c> (the default) or <c>FOR TABLE</c>.
    /// </summary>
    private (Token? For, Token? Kind, bool IsTable) ParseTemplateKind()
    {
        if (!_current.IsKeyword(NsqlKeywords.For))
        {
            return (null, null, false);
        }
        var forKeyword = Advance(); // FOR

        if (_current.IsKeyword(NsqlKeywords.Schema))
        {
            return (forKeyword, Advance(), false);
        }
        if (_current.IsKeyword(NsqlKeywords.Table))
        {
            return (forKeyword, Advance(), true);
        }
        throw Error($"Expected SCHEMA or TABLE after FOR, found '{_current.Text}'.");
    }

    private SchemaTemplateStatement ParseSchemaTemplateBody(TemplateHeader header, Identifier name)
    {
        var statements = new List<NsqlStatement>();
        _inTemplateBody = true;
        try
        {
            while (!_current.IsKeyword(NsqlKeywords.End))
            {
                if (_current.Kind == TokenKind.EndOfFile)
                {
                    throw new NsqlSyntaxException($"Unterminated template '{name.Value}'; expected END.", name.Position);
                }

                var statementDoc = TakePendingDoc();
                if (_current.IsKeyword(NsqlKeywords.End))
                {
                    break;
                }
                try
                {
                    statements.Add(ParseTemplateStatement(statementDoc));
                }
                catch (NsqlSyntaxException error)
                {
                    _errors.Add(error);
                    ResyncInTemplateBody();
                }
            }
        }
        finally
        {
            _inTemplateBody = false;
        }
        var end = Advance(); // END
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new SchemaTemplateStatement(name, statements)
        {
            Doc = header.Doc?.Text,
            DocComment = header.Doc,
            TemplateKeyword = header.Template,
            ForKeyword = header.For,
            KindKeyword = header.Kind,
            BeginKeyword = header.Begin,
            EndKeyword = end,
            SemicolonToken = semicolon,
        };
    }

    /// <summary>
    /// Parses a table template's body — comma-separated table members, the same grammar as a table body.
    /// </summary>
    private TableTemplateStatement ParseTableTemplateBody(TemplateHeader header, Identifier name)
    {
        var members = new List<TableMember>();
        var separators = new List<Token>();
        _inTemplateBody = true;
        _inTableTemplateBody = true;
        try
        {
            if (!_current.IsKeyword(NsqlKeywords.End))
            {
                var primaryKeySeen = false;
                do
                {
                    var itemDoc = TakePendingDoc();
                    members.Add(ParseTableItem(itemDoc, ref primaryKeySeen));
                }
                while (TryConsumeSeparator(TokenKind.Comma, separators));
            }
        }
        finally
        {
            _inTemplateBody = false;
            _inTableTemplateBody = false;
        }
        var end = ExpectKeyword(NsqlKeywords.End);
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new TableTemplateStatement(name, new SeparatedSyntaxList<TableMember>(members, separators))
        {
            Doc = header.Doc?.Text,
            DocComment = header.Doc,
            TemplateKeyword = header.Template,
            ForKeyword = header.For,
            KindKeyword = header.Kind,
            BeginKeyword = header.Begin,
            EndKeyword = end,
            SemicolonToken = semicolon,
        };
    }

    /// <summary>
    /// Skips to just past the next <c>;</c>, stopping short of <c>END</c> so the template's terminator
    /// still closes the body.
    /// </summary>
    private void ResyncInTemplateBody()
    {
        while (_current.Kind != TokenKind.EndOfFile && !_current.IsKeyword(NsqlKeywords.End))
        {
            if (Advance().Kind == TokenKind.Semicolon)
            {
                return;
            }
        }
    }

    private NsqlStatement ParseTemplateStatement(Token? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.Create))
        {
            return ParseCreate(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Grant))
        {
            return ParseGrant(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Script))
        {
            return ParseScript(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Rename))
        {
            return RequireObjectDirective(ParseRename(doc));
        }
        throw Error($"Unexpected '{_current.Text}' inside a template; expected a CREATE, GRANT, SCRIPT, or RENAME statement, or END.");
    }

    /// <summary>
    /// Rejects the directives whose subject a template can't own: a template describes the objects within a
    /// schema, so a schema-level directive — or a view directive, views being unavailable in a template — has
    /// no place in its body.
    /// </summary>
    private NsqlStatement RequireObjectDirective(NsqlStatement directive)
    {
        var rejected = directive switch
        {
            RenameSchemaStatement => "RENAME SCHEMA",
            RenameObjectStatement { Kind: SchemaObjectKind.View } => "RENAME VIEW",
            _ => null,
        };
        if (rejected is not null)
        {
            throw Error($"A {rejected} directive is not allowed in a template body.");
        }
        return directive;
    }

    /// <summary>
    /// Parses a template application: <c>APPLY TEMPLATE name IN SCHEMA a[, b …];</c>.
    /// </summary>
    private ApplyTemplateStatement ParseApplyTemplate(Token? doc)
    {
        var apply = Advance(); // APPLY
        var templateKeyword = ExpectKeyword(NsqlKeywords.Template);
        var name = ExpectIdentifierNode("a template name");
        var inKeyword = ExpectKeyword(NsqlKeywords.In);
        var schemaKeyword = ExpectKeyword(NsqlKeywords.Schema);

        var schemaNames = new List<Identifier>();
        var separators = new List<Token>();
        do
        {
            var schemaName = ExpectIdentifierNode("a schema name");
            if (schemaNames.Any(s => string.Equals(s.Value, schemaName.Value, StringComparison.Ordinal)))
            {
                throw new NsqlSyntaxException($"Schema '{schemaName.Value}' is listed more than once.", schemaName.Position);
            }
            schemaNames.Add(schemaName);
        }
        while (TryConsumeSeparator(TokenKind.Comma, separators));
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return new ApplyTemplateStatement(name, new SeparatedSyntaxList<Identifier>(schemaNames, separators))
        {
            Doc = doc?.Text,
            DocComment = doc,
            ApplyKeyword = apply,
            TemplateKeyword = templateKeyword,
            InKeyword = inKeyword,
            SchemaKeyword = schemaKeyword,
            SemicolonToken = semicolon,
        };
    }
}
