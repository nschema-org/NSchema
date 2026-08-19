using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Scripts;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses a SCRIPT statement.
    /// </summary>
    private ScriptStatement ParseScript(Token? doc)
    {
        var script = Advance(); // SCRIPT
        var name = ExpectIdentifierNode("a script name");
        var run = ExpectKeyword(NsqlKeywords.Run);

        RunCondition? condition = null;
        Token? conditionKeyword = null;
        if (_current.IsKeyword(NsqlKeywords.Always))
        {
            condition = RunCondition.Always;
            conditionKeyword = Advance();
        }
        else if (_current.IsKeyword(NsqlKeywords.Once))
        {
            condition = RunCondition.Once;
            conditionKeyword = Advance();
        }
        else if (_current.IsKeyword(NsqlKeywords.Unless))
        {
            throw Error("'UNLESS EXISTS' is reserved for a future release; a script runs ALWAYS (the default) or ONCE.");
        }

        var on = ExpectKeyword(NsqlKeywords.On);
        var scriptEvent = ParseScriptEvent();

        // Optional ( run_outside_transaction = true ), captured as a verbatim interior span.
        var runOutsideTransaction = false;
        Token? optionsOpen = null, optionsInterior = null, optionsClose = null;
        if (_current.Kind == TokenKind.LeftParen)
        {
            (runOutsideTransaction, optionsOpen, optionsInterior, optionsClose) = ParseScriptOptions("script");
        }

        if (!_current.IsKeyword(NsqlKeywords.As))
        {
            throw Error("Expected 'AS' before the script body.");
        }
        var asKeyword = Advance(); // AS
        var dollar = Expect(TokenKind.DollarString, "a script body as a dollar-quoted block ($$ … $$)");
        var body = StripDollarQuote(dollar.Text).Trim();
        var semicolon = Expect(TokenKind.Semicolon, "';' to end the script");

        var statement = new ScriptStatement(name, condition, scriptEvent, body, runOutsideTransaction)
        {
            Doc = doc?.Text,
            DocComment = doc,
            ScriptKeyword = script,
            RunKeyword = run,
            ConditionKeyword = conditionKeyword,
            OnKeyword = on,
            OptionsOpenParenToken = optionsOpen,
            OptionsInteriorToken = optionsInterior,
            OptionsCloseParenToken = optionsClose,
            AsKeyword = asKeyword,
            BodyToken = dollar,
            SemicolonToken = semicolon,
        };

        // A written run condition on a change event is an error, but the node stays intact so it still round-trips.
        if (statement.HasMisplacedRunCondition)
        {
            _errors.Add(new NsqlSyntaxException(
                "A run condition (ALWAYS or ONCE) is only valid on a deployment event; a change-event script runs whenever its change is planned.",
                scriptEvent.Position));
        }

        return statement;
    }

    private ScriptEventClause ParseScriptEvent()
    {
        var eventPosition = _current.Position;
        if (_current.IsAnyKeyword(NsqlKeywords.Pre, NsqlKeywords.Post))
        {
            var phaseKeyword = Advance(); // PRE | POST
            var phase = phaseKeyword.IsKeyword(NsqlKeywords.Pre) ? DeploymentPhase.Pre : DeploymentPhase.Post;
            var deploymentKeyword = ExpectKeyword(NsqlKeywords.Deployment);
            return new DeploymentEventClause(phase)
            {
                PhaseKeyword = phaseKeyword,
                DeploymentKeyword = deploymentKeyword,
            };
        }
        if (_current.IsAnyKeyword(NsqlKeywords.Add, NsqlKeywords.Alter))
        {
            var (trigger, keywords) = ParseChangeTrigger();
            var path = ParseMemberPathNode();
            return new ChangeEventClause(trigger, path) { TriggerKeywords = keywords };
        }
        throw Error("Expected a script event: 'PRE DEPLOYMENT', 'POST DEPLOYMENT', 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
    }

    private (ChangeTrigger Trigger, IReadOnlyList<Token> Keywords) ParseChangeTrigger()
    {
        if (_current.IsKeyword(NsqlKeywords.Add))
        {
            var add = Advance(); // ADD
            if (_current.IsKeyword(NsqlKeywords.Column))
            {
                return (ChangeTrigger.AddColumn, [add, Advance()]);
            }
            if (_current.IsKeyword(NsqlKeywords.Constraint))
            {
                return (ChangeTrigger.AddConstraint, [add, Advance()]);
            }
        }
        else if (_current.IsKeyword(NsqlKeywords.Alter))
        {
            var alter = Advance(); // ALTER
            var column = ExpectKeyword(NsqlKeywords.Column);
            var type = ExpectKeyword(NsqlKeywords.Type);
            return (ChangeTrigger.AlterColumnType, [alter, column, type]);
        }

        throw Error("Expected 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
    }

    /// <summary>
    /// Parses the migration target path: <c>schema.table.member</c> at the top level (the member is a column or
    /// constraint name depending on the trigger), or the unqualified <c>table.member</c> inside a template body,
    /// where the schema binds to each schema the template is applied to.
    /// </summary>
    private MemberPath ParseMemberPathNode()
    {
        var first = ExpectIdentifierNode(_inTemplateBody ? "a table name" : "a schema name");
        var firstDot = Expect(TokenKind.Dot, "'.' in the migration target path");
        var second = ExpectIdentifierNode(_inTemplateBody ? "a column or constraint name" : "a table name");

        if (_current.Kind != TokenKind.Dot)
        {
            if (_inTemplateBody)
            {
                return new MemberPath(null, first, second) { MemberDotToken = firstDot };
            }
            throw Error("Expected '.' in the migration target path (schema.table.member).");
        }

        if (_inTemplateBody)
        {
            throw Error("A migration inside a template must use an unqualified 'table.member' path; " +
                        "the schema binds to each schema the template is applied to.");
        }
        var secondDot = Advance();
        var member = ExpectIdentifierNode("a column or constraint name");
        return new MemberPath(first, second, member)
        {
            SchemaDotToken = firstDot,
            MemberDotToken = secondDot,
        };
    }

    /// <summary>
    /// Strips the opening and closing delimiters from a <see cref="TokenKind.DollarString"/>'s verbatim text
    /// (<c>$tag$ … $tag$</c>), returning its inner content. The lexer guarantees the tags are balanced.
    /// </summary>
    private static string StripDollarQuote(string text)
    {
        var tagLength = text.IndexOf('$', 1) + 1; // '$' … '$' opening tag, e.g. "$$" or "$body$"
        return text[tagLength..^tagLength];
    }

    /// <summary>
    /// Parses the optional <c>( run_outside_transaction = true )</c> clause (the cursor is on the <c>(</c>),
    /// returning the flag and the parenthesis/interior tokens. <paramref name="what"/> names the statement in errors.
    /// </summary>
    private (bool RunOutsideTransaction, Token Open, Token Interior, Token Close) ParseScriptOptions(string what)
    {
        var open = Advance(); // (
        var runOutsideTransaction = false;
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyPosition = _current.Position;
                var key = ExpectIdentifierNode($"a {what} option name").Value;
                Expect(TokenKind.Equals, "'=' after an option name");
                var valuePosition = _current.Position;
                var value = ParseSettingValue();
                switch (key.ToLowerInvariant())
                {
                    case "run_outside_transaction":
                        runOutsideTransaction = AsBoolean(value, valuePosition);
                        break;
                    default:
                        throw new NsqlSyntaxException($"Unknown {what} option '{key}'. Expected 'run_outside_transaction'.", keyPosition);
                }
            }
            while (Match(TokenKind.Comma));
        }
        var close = Expect(TokenKind.RightParen, $"')' or ',' after a {what} option");
        return (runOutsideTransaction, open, RawSpanBetween(open, close), close);
    }
}
