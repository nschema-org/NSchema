using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Schemas;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    private NsqlStatement ParseGrant(Token? doc)
    {
        var grant = Advance(); // GRANT
        var position = grant.Position;

        if (_current.IsKeyword(NsqlKeywords.Usage))
        {
            if (_inTemplateBody)
            {
                throw Error("GRANT USAGE ON SCHEMA is not supported inside a template; declare schema grants alongside the schema.");
            }
            var usage = Advance();
            var onKeyword = ExpectKeyword(NsqlKeywords.On);
            var schemaKeyword = ExpectKeyword(NsqlKeywords.Schema);
            var schema = ExpectIdentifierNode("a schema name");
            var to = ExpectKeyword(NsqlKeywords.To);
            var role = ExpectIdentifierNode("a role name");
            var semicolon = Expect(TokenKind.Semicolon, "';'");
            return new GrantSchemaUsageStatement(schema, role)
            {
                Doc = doc?.Text,
                DocComment = doc,
                GrantKeyword = grant,
                UsageKeyword = usage,
                OnKeyword = onKeyword,
                SchemaKeyword = schemaKeyword,
                ToKeyword = to,
                SemicolonToken = semicolon,
            };
        }

        var privileges = ParseTablePrivileges();
        var grantOn = ExpectKeyword(NsqlKeywords.On);
        var on = ParseQualifiedNameNode();
        var grantTo = ExpectKeyword(NsqlKeywords.To);
        var grantee = ExpectIdentifierNode("a role name");
        var grantSemicolon = Expect(TokenKind.Semicolon, "';'");
        return new GrantTableStatement(privileges, on, grantee)
        {
            Doc = doc?.Text,
            DocComment = doc,
            GrantKeyword = grant,
            OnKeyword = grantOn,
            ToKeyword = grantTo,
            SemicolonToken = grantSemicolon,
        };
    }

    private SeparatedSyntaxList<Privilege> ParseTablePrivileges()
    {
        var privileges = new List<Privilege> { ParseTablePrivilege() };
        var separators = new List<Token>();
        while (TryConsumeSeparator(TokenKind.Comma, separators))
        {
            privileges.Add(ParseTablePrivilege());
        }
        return new SeparatedSyntaxList<Privilege>(privileges, separators);
    }

    private Privilege ParseTablePrivilege()
    {
        if (!_current.IsAnyKeyword(NsqlKeywords.Select, NsqlKeywords.Insert, NsqlKeywords.Update, NsqlKeywords.Delete))
        {
            throw Error($"Expected a privilege (SELECT, INSERT, UPDATE, DELETE), found '{_current.Text}'.");
        }
        var token = Advance();
        return new Privilege(token);
    }
}
