using NSchema.Model;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

/// <summary>
/// Recursive-descent parser for the NSchema language, producing the syntax tree.
/// The AST is then assembled into a <see cref="ProjectDefinition"/> model.
/// </summary>
internal sealed partial class NsqlParser
{
    private readonly NsqlLexer _lexer;
    private Token _current;

    // True while parsing a template body, where unqualified names are legal (they bind to the applied
    // schema at projection) and template-restricted statements are rejected.
    private bool _inTemplateBody;

    // True while parsing a FOR TABLE template's member body, where INCLUDE members are rejected
    // (a table template cannot include another).
    private bool _inTableTemplateBody;

    private readonly List<NsqlSyntaxException> _errors = [];

    /// <summary>
    /// The syntax errors collected while parsing. The parser recovers at statement boundaries, so one
    /// parse reports every error in the document; the returned tree carries the statements that parsed.
    /// </summary>
    public IReadOnlyList<NsqlSyntaxException> Errors => _errors;

    public NsqlParser(string source)
    {
        _lexer = new NsqlLexer(source);
        _current = _lexer.Next();
    }

    /// <summary>
    /// Parses the whole document under the project grammar.
    /// </summary>
    public NsqlDocument Parse()
    {
        var statements = ParseDocumentBody(ParseStatement);
        // _current is the end-of-file token, whose leading trivia is the file's trailing whitespace/comments.
        return new NsqlDocument(statements) { EndOfFile = _current };
    }

    /// <summary>
    /// The document-level parse loop shared by the project and configuration grammars: statements to end of
    /// file, each taking the doc-comments before it (last one wins; one dangling at EOF attaches to nothing).
    /// </summary>
    private List<T> ParseDocumentBody<T>(Func<Token?, T> parseStatement)
    {
        var statements = new List<T>();

        while (_current.Kind != TokenKind.EndOfFile)
        {
            var recoveryStart = _current;
            var doc = TakePendingDoc();
            if (_current.Kind == TokenKind.EndOfFile)
            {
                // Doc-comment(s) with no statement after them: keep them as skipped trivia so they still round-trip.
                AttachSkipped(recoveryStart);
                break;
            }

            try
            {
                statements.Add(parseStatement(doc));
            }
            catch (NsqlSyntaxException error)
            {
                // Record and resync to the next statement boundary, so one parse reports every error.
                _errors.Add(error);
                Resync();
                AttachSkipped(recoveryStart);
            }
        }

        return statements;
    }

    /// <summary>
    /// Attaches the source from <paramref name="from"/> (its leading trivia included) up to the cursor as a single
    /// <see cref="TriviaKind.Skipped"/> trivia on the cursor token, so tokens discarded during error recovery still
    /// round-trip. Roslyn's <c>SkippedTokensTrivia</c> in spirit: skipped input becomes leading trivia of the next token.
    /// </summary>
    private void AttachSkipped(Token from)
    {
        var start = from.Position.Offset - TriviaWidth(from.Leading);
        var end = _current.Position.Offset - TriviaWidth(_current.Leading);
        if (start >= end)
        {
            return;
        }

        var skipped = new Trivia(TriviaKind.Skipped, _lexer.Slice(start, end), from.Position);
        var leading = new List<Trivia>(_current.Leading.Count + 1) { skipped };
        leading.AddRange(_current.Leading);
        _current = _current with { Leading = leading };
    }

    private static int TriviaWidth(IReadOnlyList<Trivia> trivia)
    {
        var width = 0;
        foreach (var item in trivia)
        {
            width += item.Text.Length;
        }
        return width;
    }

    /// <summary>
    /// Skips to just past the next top-level <c>;</c> (the statement boundary). Strings and dollar-quoted
    /// bodies are single tokens, so a <c>;</c> inside them is never structural.
    /// </summary>
    private void Resync()
    {
        while (_current.Kind != TokenKind.EndOfFile)
        {
            if (Advance().Kind == TokenKind.Semicolon)
            {
                return;
            }
        }
    }

    // Dispatches on NsqlKeywords.StatementOpeners — a new opener is added there first, so the formatter
    // recognizes it as a statement boundary.
    private NsqlStatement ParseStatement(Token? doc)
    {
        if (_current.IsKeyword(NsqlKeywords.Create))
        {
            return ParseCreate(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Grant))
        {
            return ParseGrant(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Template))
        {
            return ParseTemplate(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Apply))
        {
            return ParseApplyTemplate(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Script))
        {
            return ParseScript(doc);
        }
        if (_current.IsKeyword(NsqlKeywords.Rename))
        {
            return ParseRename(doc);
        }
        // One grammar: configuration and lock blocks parse alongside declarations and directives. Which kinds a
        // given file admits is a file-type rule the reader enforces after parsing, not a property of the grammar.
        if (CurrentSettingsKeyword() is { } blockKeyword)
        {
            return ParseSettingsStatement(blockKeyword, doc);
        }
        if (_current.Kind == TokenKind.Identifier)
        {
            throw Error($"Unknown statement '{_current.Text}'.");
        }
        throw Error($"Unexpected '{_current.Text}'; expected a statement.");
    }

    // --- name nodes -------------------------------------------------------------

    private Identifier ExpectIdentifierNode(string what)
    {
        if (!_current.IsName)
        {
            throw Error($"Expected {what}.");
        }
        var token = Advance();
        return new Identifier(token);
    }

    private QualifiedName ParseQualifiedNameNode()
    {
        var first = ExpectIdentifierNode("a schema name");
        if (_current.Kind != TokenKind.Dot)
        {
            // Inside a template body an unqualified name is stored as written; projection binds it.
            if (_inTemplateBody)
            {
                return new QualifiedName(null, first);
            }
            throw Error("Expected '.'.");
        }
        var dot = Advance();
        var name = ExpectIdentifierNode("a table name");
        return new QualifiedName(first, name) { DotToken = dot };
    }

    /// <summary>
    /// Parses a whole address fragment to end of input.
    /// </summary>
    public Address ParseAddress()
    {
        var segments = new List<Identifier> { ExpectIdentifierNode("an identifier") };
        while (_current.Kind == TokenKind.Dot)
        {
            if (segments.Count == 3)
            {
                throw Error("An address has at most three parts: schema.object.member.");
            }
            Advance();
            segments.Add(ExpectIdentifierNode("an identifier"));
        }
        if (_current.Kind != TokenKind.EndOfFile)
        {
            throw Error($"Unexpected '{_current.Text}'.");
        }
        return segments switch
        {
            [var schema] => DatabaseAddress.Schema(schema.Value),
            [var schema, var name] => new ObjectAddress(schema.Value, name.Value),
            [var schema, var obj, var member] => new MemberAddress(schema.Value, obj.Value, member.Value),
            _ => throw Error("An address has at most three parts: schema.object.member."),
        };
    }

    // --- token cursor helpers -----------------------------------------------------

    private Token Advance()
    {
        var consumed = _current;
        _current = _lexer.Next();
        return consumed;
    }

    private Token Expect(TokenKind kind, string what)
    {
        if (_current.Kind != kind)
        {
            throw Error($"Expected {what}.");
        }
        return Advance();
    }

    private Token ExpectKeyword(string keyword)
    {
        if (!_current.IsKeyword(keyword))
        {
            throw Error($"Expected '{keyword}'.");
        }
        return Advance();
    }

    private string ExpectInteger() => Expect(TokenKind.Integer, "an integer").Text;

    private long ExpectIntegerValue() => long.Parse(ExpectInteger());

    private long ExpectSignedIntegerValue()
    {
        var negative = Match(TokenKind.Minus);
        var value = ExpectIntegerValue();
        return negative ? -value : value;
    }

    private bool Match(TokenKind kind)
    {
        if (_current.Kind != kind)
        {
            return false;
        }
        Advance();
        return true;
    }

    /// <summary>Consumes a separator token (adding it to <paramref name="separators"/>) when the cursor is on one.</summary>
    private bool TryConsumeSeparator(TokenKind kind, List<Token> separators)
    {
        if (_current.Kind != kind)
        {
            return false;
        }
        separators.Add(Advance());
        return true;
    }

    /// <summary>Consumes any doc-comments at the cursor, returning the last one's token (or null).</summary>
    private Token? TakePendingDoc()
    {
        Token? doc = null;
        while (_current.Kind == TokenKind.DocComment)
        {
            doc = _current;
            Advance();
        }
        return doc;
    }

    private NsqlSyntaxException Error(string message) => new(message, _current.Position);

    // --- opaque-span capture --------------------------------------------------
    //
    // The lexer is context-free, so opaque expression text (view bodies, default expressions, routine definitions,
    // …) is recovered here by slicing the source between the offsets of the tokens we consume, rather than by the
    // lexer reading raw text on the parser's behalf. Strings and dollar-quoted bodies are single tokens, so a
    // terminator inside them is never structural; parentheses are balanced so a depth-zero terminator wins.

    /// <summary>
    /// Captures the verbatim source of a balanced parenthesised group at the cursor (which must be <c>(</c>),
    /// returning the inner text trimmed (possibly empty). Consumes through the matching <c>)</c>.
    /// </summary>
    private string CaptureParenthesized() => CaptureParenthesizedToken().Inner;

    /// <summary>
    /// As <see cref="CaptureParenthesized"/>, but also returns the <c>(</c> and <c>)</c> tokens and the interior as a
    /// verbatim <see cref="TokenKind.RawSpan"/> token (each paren's own trivia excluded), so the group reprints exactly.
    /// </summary>
    private (Token Open, string Inner, Token Span, Token Close) CaptureParenthesizedToken()
    {
        var open = Expect(TokenKind.LeftParen, "'(' to begin an expression");
        var innerStart = open.Position.Offset + 1; // just past '('
        var depth = 1;
        while (true)
        {
            if (_current.Kind == TokenKind.EndOfFile)
            {
                throw new NsqlSyntaxException("Unterminated expression", open.Position);
            }
            if (_current.Kind == TokenKind.LeftParen)
            {
                depth++;
            }
            else if (_current.Kind == TokenKind.RightParen && --depth == 0)
            {
                var close = _current;
                var inner = _lexer.Slice(innerStart, close.Position.Offset).Trim();
                var span = RawSpanBetween(open, close);
                Advance(); // consume ')'
                return (open, inner, span, close);
            }
            Advance();
        }
    }

    /// <summary>
    /// Mints a <see cref="TokenKind.RawSpan"/> token for the verbatim text strictly between two tokens (each
    /// token's own trivia excluded, since each prints its own), for a bracketed opaque region.
    /// </summary>
    private Token RawSpanBetween(Token open, Token close)
    {
        var openTrailing = 0;
        foreach (var trivia in open.Trailing)
        {
            openTrailing += trivia.Text.Length;
        }
        var closeLeading = 0;
        foreach (var trivia in close.Leading)
        {
            closeLeading += trivia.Text.Length;
        }
        var raw = _lexer.Slice(open.Position.Offset + open.Raw.Length + openTrailing, close.Position.Offset - closeLeading);
        return new Token(TokenKind.RawSpan, raw.Trim(), open.Position) { Raw = raw };
    }

    /// <summary>
    /// Mints a <see cref="TokenKind.RawSpan"/> token spanning already-consumed tokens from
    /// <paramref name="startToken"/> up to (but excluding) <paramref name="terminator"/> and its leading trivia.
    /// </summary>
    private Token RawSpanFrom(Token startToken, Token terminator)
    {
        var terminatorLeading = 0;
        foreach (var trivia in terminator.Leading)
        {
            terminatorLeading += trivia.Text.Length;
        }
        var raw = _lexer.Slice(startToken.Position.Offset, terminator.Position.Offset - terminatorLeading);
        return new Token(TokenKind.RawSpan, raw.Trim(), startToken.Position) { Raw = raw, Leading = startToken.Leading };
    }

    /// <summary>
    /// Captures an opaque body (a routine definition or view body): either a single dollar-quoted block —
    /// delimiters stripped, so a body holding top-level <c>;</c>s survives — or the verbatim source up to the
    /// terminating <c>;</c>.
    /// </summary>
    private (string Text, Token Span) CaptureOpaqueBody(string what)
    {
        if (_current.Kind == TokenKind.DollarString)
        {
            var dollar = Advance();
            return (StripDollarQuote(dollar.Text).Trim(), dollar);
        }

        return CaptureRawSpanToken(what, [TokenKind.Semicolon]);
    }

    /// <summary>
    /// Captures the verbatim source from the cursor up to — but not consuming — a depth-zero terminator among the
    /// <paramref name="terminators"/> token kinds.
    /// Returns it trimmed. Throws <c>Expected {what}</c> when the span is empty unless <paramref name="allowEmpty"/>.
    /// </summary>
    private string CaptureRawSpan(string what, ReadOnlySpan<TokenKind> terminators, bool allowEmpty = false) =>
        CaptureRawSpanToken(what, terminators, allowEmpty).Text;

    /// <summary>
    /// As <see cref="CaptureRawSpan"/>, but also returns the span as a verbatim <see cref="TokenKind.RawSpan"/> token
    /// (the exact source with the span's leading trivia, so the opaque region reprints byte-for-byte). The raw text
    /// ends at the terminator's raw start — excluding the terminator's own leading trivia, which the terminator prints.
    /// </summary>
    private (string Text, Token Span) CaptureRawSpanToken(string what, ReadOnlySpan<TokenKind> terminators, bool allowEmpty = false)
    {
        var startToken = _current;
        var depth = 0;
        while (true)
        {
            var kind = _current.Kind;
            if (kind == TokenKind.EndOfFile)
            {
                break;
            }
            if (kind == TokenKind.LeftParen)
            {
                depth++;
            }
            else if (kind == TokenKind.RightParen)
            {
                if (depth > 0)
                {
                    depth--;
                }
                else if (terminators.Contains(TokenKind.RightParen))
                {
                    break;
                }
            }
            else if (depth == 0)
            {
                if (terminators.Contains(kind))
                {
                    break;
                }
            }
            Advance();
        }

        var terminator = _current;
        var text = _lexer.Slice(startToken.Position.Offset, terminator.Position.Offset).Trim();
        if (!allowEmpty && text.Length == 0)
        {
            throw new NsqlSyntaxException($"Expected {what}", startToken.Position);
        }

        return (text, RawSpanFrom(startToken, terminator));
    }
}
