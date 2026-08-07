using System.Text;
using NSchema.Model;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Settings;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

/// <summary>
/// Writes NSchema SQL. The counterpart to <see cref="NsqlReader"/>.
/// </summary>
public static class NsqlWriter
{
    private const int MaxBlankLines = 1;
    private const string Indent = "  ";

    /// <summary>
    /// Reformats <paramref name="source"/> as canonical-layout NSchema DDL. The formatted text is always the value
    /// (formatting cannot fail); syntax errors ride as error diagnostics and each statement a rewrite would change
    /// rides as a warning — so a caller can rewrite (<see cref="Result{T}.Value"/>) or check (the diagnostics).
    /// </summary>
    /// <param name="source">The DDL source text to format.</param>
    public static Result<string, NsqlDiagnostic> Format(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var read = NsqlReader.Read(source);
        if (read.Value is not { } document)
        {
            return Result<string, NsqlDiagnostic>.From(string.Empty, read.Diagnostics);
        }

        DiagnosticCollection<NsqlDiagnostic> diagnostics = [.. read.Diagnostics];
        for (var i = 0; i < document.Statements.Count; i++)
        {
            var statement = document.Statements[i];
            if (!IsFormatted(statement, first: i == 0))
            {
                diagnostics.Add(NsqlDiagnostics.Formatting(statement.Position));
            }
        }

        return diagnostics.ToResult(Render(document));
    }

    /// <summary>
    /// Renders an already-built document tree to canonical text — no re-parse. A synthetic (factory-built) tree
    /// carries no trivia, so the output is syntactically valid but unstyled beyond the structural rules; a parsed
    /// tree formats exactly as <see cref="Format(string)"/>'s value.
    /// </summary>
    /// <param name="document">The document tree to print.</param>
    public static string Write(NsqlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Render(document);
    }

    /// <summary>Writes a domain model as canonical NSchema source.</summary>
    /// <param name="database">The schema to write.</param>
    public static string Write(Database database) => Render(SyntaxBuilder.Build(database));

    /// <summary>Writes a whole project (schema plus directives) as canonical NSchema source.</summary>
    /// <param name="database">The schema to write.</param>
    /// <param name="directives">The directives to write after the schema.</param>
    public static string Write(Database database, ProjectDirectives directives) => Render(SyntaxBuilder.Build(database, directives));

    /// <summary>Renders the whole document to canonical text, ending in a single newline (empty input yields "").</summary>
    private static string Render(NsqlDocument document)
    {
        var items = document.Statements.Select(Render).ToList();

        // A run of trailing comments with no statement after them (on the end-of-file token) is a final item.
        var trailing = document.EndOfFile is { } eof ? CommentLines(eof.Leading) : [];
        if (trailing.Count > 0)
        {
            items.Add(new Item { Leading = trailing, Body = null });
        }

        var sb = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                // Put a blank line between statements.
                sb.Append("\n\n");
            }
            AppendItem(sb, items[i]);
        }

        var text = sb.ToString().TrimEnd('\n');
        return text.Length == 0 ? string.Empty : text + "\n";
    }

    /// <summary>
    /// Whether a statement's layout is already canonical: the right number of blank lines before it (none for the
    /// first statement or one that hugs its subject, one otherwise) and a body that renders to exactly its source.
    /// </summary>
    private static bool IsFormatted(NsqlStatement statement, bool first) =>
        LeadingBlankLines(statement) == (first ? 0 : 1)
        && RenderStatement(statement) == statement.ToSource().Trim();

    /// <summary>The canonical rendering of one statement (leading comments, body, trailing comment), no outer blanks.</summary>
    private static string RenderStatement(NsqlStatement statement)
    {
        var sb = new StringBuilder();
        AppendItem(sb, Render(statement));
        return sb.ToString().Trim();
    }

    /// <summary>The blank lines separating a statement (or its leading comment) from what precedes it.</summary>
    private static int LeadingBlankLines(NsqlStatement statement)
    {
        var leading = statement.DocComment is { } doc ? doc.Leading : FirstToken(statement).Leading;
        var count = 0;
        foreach (var item in leading)
        {
            if (item.Kind == TriviaKind.EndOfLine)
            {
                count++;
            }
            else if (item.IsComment)
            {
                break;
            }
        }
        return count;
    }

    // --- one statement --------------------------------------------------------

    private static Item Render(NsqlStatement statement)
    {
        var (leading, blanksBeforeBody) = LeadingRegion(statement);
        return new Item
        {
            Source = statement,
            Leading = leading,
            BlanksBeforeBody = blanksBeforeBody,
            Body = Body(statement),
            Trailing = TrailingComment(LastToken(statement)),
        };
    }

    private static void AppendItem(StringBuilder sb, Item item)
    {
        foreach (var lead in item.Leading)
        {
            AppendBlankLines(sb, lead.BlanksBefore);
            sb.Append(lead.Text).Append('\n');
        }
        if (item.Body is { } body)
        {
            AppendBlankLines(sb, item.BlanksBeforeBody);
            sb.Append(body);
            if (item.Trailing is { } trailing)
            {
                sb.Append(Indent).Append(trailing);
            }
        }
    }

    private static string Body(NsqlStatement statement) => statement switch
    {
        CreateTableStatement table => Broken(Header(statement, table.OpenParenToken), RenderMembers(table.Members, table.CloseParenToken)) + NsqlSymbols.Semicolon,
        SettingsStatement settings => Broken(Header(statement, settings.OpenParenToken), RenderMembers(settings.Settings, settings.CloseParenToken)) + NsqlSymbols.Semicolon,
        SchemaTemplateStatement template => Template(template),
        TableTemplateStatement template => Template(template),
        _ => Verbatim(statement),
    };

    /// <summary>The statement's significant tokens (doc-comment, leading, and trailing comments excluded), verbatim.</summary>
    private static string Verbatim(NsqlStatement statement) => EmitTokens(FlattenTokens(statement));

    /// <summary>The header of a broken statement — everything up to (not including) the open paren — verbatim.</summary>
    private static string Header(NsqlNode statement, Token? open)
    {
        var tokens = FlattenTokens(statement);
        if (open is { } paren)
        {
            tokens = [.. tokens.TakeWhile(t => t != paren)];
        }
        return EmitTokens(tokens);
    }

    /// <summary>Renders each element of a separated list to a <see cref="Member"/>, taking the trailing comment from
    /// the element or the comma that follows it (a same-line comment after either trails the member).</summary>
    private static List<Member> RenderMembers<T>(SeparatedSyntaxList<T> list, Token? close) where T : NsqlNode
    {
        var members = new List<Member>();
        for (var i = 0; i < list.Count; i++)
        {
            var separator = i < list.Separators.Count ? list.Separators[i] : (Token?)null;
            members.Add(new Member
            {
                Leading = CommentLines(FirstToken(list[i]).Leading, (list[i] as TableMember)?.DocComment),
                Content = NodeText(list[i]),
                Trailing = TrailingComment(LastToken(list[i])) ?? (separator is { } comma ? TrailingComment(comma) : null),
            });
        }

        // Comments between the last member and the closing paren are dangling own-line comments; each keeps its line.
        if (close is { } paren)
        {
            foreach (var lead in CommentLines(paren.Leading))
            {
                members.Add(new Member { Leading = [lead], Content = null });
            }
        }
        return members;
    }

    /// <summary>Emits a broken <c>header (\n  member,\n  …\n)</c> body.</summary>
    private static string Broken(string header, List<Member> members)
    {
        var list = members;
        var lastContent = list.FindLastIndex(m => m.Content is not null);

        var sb = new StringBuilder(header).Append(" (");
        for (var i = 0; i < list.Count; i++)
        {
            var member = list[i];
            foreach (var comment in member.Leading)
            {
                // A doc-comment is one lead spanning several lines, so the indent goes on each of them.
                foreach (var line in comment.Text.Split('\n'))
                {
                    sb.Append('\n').Append(Indent).Append(line);
                }
            }
            if (member.Content is { } content)
            {
                sb.Append('\n').Append(Indent).Append(content);
                if (i < lastContent)
                {
                    sb.Append(',');
                }
                if (member.Trailing is { } trailing)
                {
                    sb.Append(Indent).Append(trailing);
                }
            }
        }
        return sb.Append("\n)").ToString();
    }

    /// <summary>Emits a <c>TEMPLATE … BEGIN … END;</c>: header verbatim, BEGIN/END on their own lines, body indented.</summary>
    private static string Template(TemplateStatement template)
    {
        var header = Header(template, template.BeginKeyword);
        var sb = new StringBuilder(header).Append("\nBEGIN");

        if (template is TableTemplateStatement table)
        {
            var body = Broken("", RenderMembers(table.Members, close: null));
            // Drop the synthetic " (" head and trailing "\n)" the member renderer adds — a template body has neither.
            sb.Append(body[" (".Length..^"\n)".Length]);
        }
        else if (template is SchemaTemplateStatement schema)
        {
            // The body is a mini-document: inner statements separated by a blank line, then indented one level.
            var items = schema.Statements.Select(Render).ToList();
            if (items.Count > 0)
            {
                var inner = new StringBuilder();
                for (var i = 0; i < items.Count; i++)
                {
                    if (i > 0)
                    {
                        inner.Append("\n\n");
                    }
                    AppendItem(inner, items[i]);
                }
                // Indent each body line one level — but never inside a dollar-quoted block, so a script body stays
                // put (and re-formatting is idempotent).
                string? openTag = null;
                foreach (var line in inner.ToString().Split('\n'))
                {
                    sb.Append('\n');
                    if (line.Length > 0)
                    {
                        if (openTag is null)
                        {
                            sb.Append(Indent);
                        }
                        sb.Append(line);
                    }
                    openTag = AdvanceDollarQuoteState(line, openTag);
                }
            }
        }

        return sb.Append("\nEND;").ToString();
    }

    // --- token emission -------------------------------------------------------

    /// <summary>All the node's tokens in source order, doc-comments excluded (they lead the statement, placed separately).</summary>
    private static List<Token> FlattenTokens(NsqlNode node)
    {
        var tokens = new List<Token>();
        Collect(node);
        return tokens;

        void Collect(NsqlNode current)
        {
            foreach (var child in current.Children)
            {
                if (child.AsToken() is { } token)
                {
                    if (token.Kind != TokenKind.DocComment)
                    {
                        tokens.Add(token);
                    }
                }
                else if (child.AsNode() is { } childNode)
                {
                    Collect(childNode);
                }
            }
        }
    }

    /// <summary>
    /// Emits a token run verbatim — raw text and interior trivia (whitespace and comments) — dropping only the
    /// leading trivia of the first token and the trailing trivia of the last (the statement's edges, placed
    /// separately as leading/trailing comments), then trimming.
    /// </summary>
    private static string EmitTokens(IReadOnlyList<Token> tokens)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (i > 0)
            {
                // A synthetic (factory-built) token carries no trivia and no source position, so an empty gap before
                // it gets a synthesized separator — otherwise the tokens would jam together. Parsed trees keep their
                // own trivia (empty gaps like `app.users` stay tight).
                if (tokens[i - 1].Trailing.Count == 0 && tokens[i].Leading.Count == 0 && tokens[i].Position.Line == 0)
                {
                    sb.Append(Separator(tokens[i - 1], tokens[i]));
                }
                else
                {
                    AppendTrivia(sb, tokens[i].Leading);
                }
            }
            sb.Append(tokens[i].Raw);
            if (i < tokens.Count - 1)
            {
                AppendTrivia(sb, tokens[i].Trailing);
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// The separator synthesized in an empty gap between two synthetic tokens: a space, but tight where SQL reads
    /// wrong with one — no space before a closer or separator, none after an opener, none either side of a dot, and
    /// none before a call/argument paren.
    /// </summary>
    private static string Separator(Token left, Token right)
    {
        if (right.Kind is TokenKind.Semicolon or TokenKind.Comma or TokenKind.RightParen or TokenKind.RightBrace or TokenKind.Dot)
        {
            return "";
        }
        if (left.Kind is TokenKind.LeftParen or TokenKind.LeftBrace or TokenKind.Dot)
        {
            return "";
        }
        if (right.Kind is TokenKind.LeftParen)
        {
            // A call/argument paren hugs a preceding name (`decimal(18,2)`, `fn(a)`); after a keyword or any other
            // token (`AS (…)`, `KEY (…)`, `, (…)`) it takes a space.
            var hugsName = left.Kind is TokenKind.QuotedIdentifier
                || (left.Kind is TokenKind.Identifier && !NsqlKeywords.All.Contains(left.Text));
            return hugsName ? "" : " ";
        }
        return " ";
    }

    private static string NodeText(NsqlNode node) => EmitTokens(FlattenTokens(node));

    private static void AppendTrivia(StringBuilder sb, IReadOnlyList<Trivia> trivia)
    {
        foreach (var item in trivia)
        {
            sb.Append(item.Text);
        }
    }

    // --- comments -------------------------------------------------------------

    /// <summary>
    /// The comment lines that lead a statement or member — the source comments in its leading trivia, then its
    /// doc-comment (if any) — each with the (capped) blank lines the source had before it.
    /// </summary>
    private static List<Lead> CommentLines(IReadOnlyList<Trivia> trivia, Token? doc = null)
    {
        var leads = new List<Lead>();
        var newlines = 0;
        var seen = false;
        foreach (var item in trivia)
        {
            if (item.Kind == TriviaKind.EndOfLine)
            {
                newlines++;
            }
            else if (item.IsComment)
            {
                leads.Add(new Lead(FormatComment(item.Text), seen ? Math.Max(0, newlines - 1) : 0));
                newlines = 0;
                seen = true;
            }
        }
        if (doc is { } comment)
        {
            leads.Add(new Lead(FormatDoc(comment.Text), seen ? Math.Max(0, newlines - 1) : 0));
        }
        return leads;
    }

    /// <summary>A doc-comment re-emitted in canonical <c>--- line</c> form, one line per merged line.</summary>
    private static string FormatDoc(string text) => string.Join('\n', text.Split('\n').Select(line => "--- " + line));

    /// <summary>
    /// The leading comment lines of a statement (source comments then its doc-comment), and the blank lines to keep
    /// between them and the statement body.
    /// </summary>
    private static (List<Lead> Leads, int BlanksBeforeBody) LeadingRegion(NsqlStatement statement)
    {
        var leads = new List<Lead>();
        var newlines = 0;
        var seen = false;

        void Scan(IReadOnlyList<Trivia> trivia)
        {
            foreach (var item in trivia)
            {
                if (item.Kind == TriviaKind.EndOfLine)
                {
                    newlines++;
                }
                else if (item.IsComment)
                {
                    leads.Add(new Lead(FormatComment(item.Text), seen ? Math.Max(0, newlines - 1) : 0));
                    newlines = 0;
                    seen = true;
                }
            }
        }

        if (statement.DocComment is { } doc)
        {
            Scan(doc.Leading);
            leads.Add(new Lead(FormatDoc(doc.Text), seen ? Math.Max(0, newlines - 1) : 0));
            newlines = 0;
            seen = true;
            Scan(FirstSignificantToken(statement).Leading);
        }
        else
        {
            Scan(FirstToken(statement).Leading);
        }

        return (leads, seen ? Math.Max(0, newlines - 1) : 0);
    }

    /// <summary>The trailing line comment on <paramref name="token"/> (on the same line as it, before any newline), or null.</summary>
    private static string? TrailingComment(Token token)
    {
        Trivia? found = null;
        foreach (var item in token.Trailing)
        {
            if (item.Kind == TriviaKind.EndOfLine)
            {
                break;
            }
            if (item.IsComment)
            {
                found = item;
            }
        }
        return found is { } comment ? FormatComment(comment.Text) : null;
    }

    private static string FormatComment(string text) => text.TrimEnd();

    // --- blank lines / tree helpers -------------------------------------------

    private static void AppendBlankLines(StringBuilder sb, int count)
    {
        for (var i = 0; i < Math.Min(count, MaxBlankLines); i++)
        {
            sb.Append('\n');
        }
    }

    private static Token FirstToken(NsqlNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.AsToken() is { } token)
            {
                return token;
            }
            return child.AsNode() is { } childNode ? FirstToken(childNode) : default;
        }
        return default;
    }

    /// <summary>The first token that is not a doc-comment (the statement's leading keyword).</summary>
    private static Token FirstSignificantToken(NsqlNode node) => FlattenTokens(node) is [var first, ..] ? first : default;

    private static Token LastToken(NsqlNode node)
    {
        Token last = default;
        foreach (var child in node.Children)
        {
            last = child.AsToken()
                ?? (child.AsNode() is { } childNode ? LastToken(childNode) : default);
        }
        return last;
    }

    /// <summary>
    /// Tracks whether a line ends inside a dollar-quoted block. A body only closes on its own tag, so <c>$$</c>
    /// inside a <c>$body$</c> block stays interior text.
    /// </summary>
    private static string? AdvanceDollarQuoteState(string line, string? openTag)
    {
        var i = 0;
        while (i < line.Length)
        {
            if (openTag is null)
            {
                var open = FindDollarTag(line, i);
                if (open is null)
                {
                    break;
                }
                openTag = open.Value.Tag;
                i = open.Value.End;
            }
            else
            {
                var close = line.IndexOf(openTag, i, StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }
                i = close + openTag.Length;
                openTag = null;
            }
        }
        return openTag;
    }

    /// <summary>Finds the next <c>$tag$</c> delimiter at or after <paramref name="from"/>, returning it and the index just past it.</summary>
    private static (string Tag, int End)? FindDollarTag(string line, int from)
    {
        for (var i = line.IndexOf('$', from); i >= 0; i = line.IndexOf('$', i + 1))
        {
            var close = i + 1;
            while (close < line.Length && (char.IsLetterOrDigit(line[close]) || line[close] == '_'))
            {
                close++;
            }
            if (close < line.Length && line[close] == '$')
            {
                return (line[i..(close + 1)], close + 1);
            }
        }
        return null;
    }

    private sealed class Item
    {
        public NsqlStatement? Source { get; init; }
        public required List<Lead> Leading { get; init; }
        public required string? Body { get; init; }
        public int BlanksBeforeBody { get; init; }
        public string? Trailing { get; init; }
    }

    private readonly record struct Lead(string Text, int BlanksBefore);

    private sealed class Member
    {
        public required List<Lead> Leading { get; init; }
        public string? Content { get; init; }
        public string? Trailing { get; init; }
    }
}
