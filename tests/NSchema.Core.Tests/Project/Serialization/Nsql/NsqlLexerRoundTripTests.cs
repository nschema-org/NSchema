using System.Text;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The load-bearing lexer property: every character of the source is emitted, so concatenating each token's
/// leading trivia, raw text, and trailing trivia reproduces the source byte-for-byte.
/// </summary>
public sealed class NsqlLexerRoundTripTests
{
    /// <summary>Reconstructs the source from the token stream (leading + raw + trailing over every token, EOF included).</summary>
    private static string Reconstruct(string source)
    {
        var lexer = new NsqlLexer(source);
        var sb = new StringBuilder();
        while (true)
        {
            var token = lexer.Next();
            foreach (var trivia in token.Leading)
            {
                sb.Append(trivia.Text);
            }
            sb.Append(token.Raw);
            foreach (var trivia in token.Trailing)
            {
                sb.Append(trivia.Text);
            }
            if (token.Kind == TokenKind.EndOfFile)
            {
                return sb.ToString();
            }
        }
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Lex_ReconstructsSourceByteForByte(string source)
    {
        // Act
        var reconstructed = Reconstruct(source);

        // Assert
        reconstructed.ShouldBe(source);
    }

    public static TheoryData<string> Corpus()
    {
        var data = new TheoryData<string>();
        foreach (var source in Sources())
        {
            data.Add(source);
        }
        return data;
    }

    private static IEnumerable<string> Sources()
    {
        // Empty and whitespace-only inputs.
        yield return "";
        yield return "   ";
        yield return "\n\n";
        yield return "\t \r\n  \n";

        // Bare tokens with surrounding whitespace, tabs, and no trailing newline.
        yield return "users";
        yield return "  users  ";
        yield return "\tapp.users\t";
        yield return "CREATE\n  users";

        // Punctuation and literals, verbatim.
        yield return "(){},;.=-";
        yield return "'it''s here'";
        yield return "\"Order Details\"";
        yield return "[Order Details]";
        yield return "[weird ]]col]]]";
        yield return "'{}'::text[]";
        yield return "1024";
        yield return "a > b && c";

        // Line comments in every position, including at end of file without a trailing newline.
        yield return "-- a note\nusers";
        yield return "users -- trailing note\n;";
        yield return "-- lonely comment";
        yield return "users;   -- after semicolon, spaces before newline   \n";

        // Block comments, mid-line and empty.
        yield return "a /* mid */ b";
        yield return "a /**/ b";
        yield return "/* leading */\ncreate schema app;";
        yield return "create /* inline\nspanning lines */ schema app;";

        // Doc-comments (tokens, not trivia), merged and indented.
        yield return "--- All users.\nusers";
        yield return "--- Line one.\n--- Line two.\nusers";
        yield return "  --- indented doc\n  users";
        yield return "/** block doc */ users";

        // Blank-line runs and mixed CRLF/LF line endings.
        yield return "a\n\n\nb";
        yield return "create schema a;\r\ncreate schema b;\r\n";
        yield return "-- crlf comment\r\nusers\r\n";

        // Dollar-quoted bodies keep their internal newlines and semicolons verbatim.
        yield return "$$ SELECT 1; $$";
        yield return "script s run on pre deployment as $$\nSELECT 1;\n$$;";
        yield return "$body$ line one\nline two; $$ $body$";

        // The messy real documents the formatter snapshots exercise.
        foreach (var source in MessyDocuments())
        {
            yield return source;
        }
    }

    private static IEnumerable<string> MessyDocuments()
    {
        yield return
            """
            create schema billing;
              create schema ordering;
            --- Standard audit columns for every table.
            template audit_columns for table begin
                created_at datetimeoffset not null,
              updated_at datetimeoffset not null,  -- touched on write
                  constraint chk_audit check (updated_at >= created_at)
             end;
            --- Transactional outbox, one per subdomain schema.
            template outbox begin
                create enum outbox_status('pending','sent');
              create table outbox(
                  id uuid not null,
                status outbox_status not null,  -- current delivery state
                    payload text not null,
                  include audit_columns,
                constraint pk_outbox primary key(id));
              -- covering index for the dispatcher
             create index ix_outbox_status on outbox(status);
                grant select, insert on outbox to svc;
                end;
                apply template outbox in schema billing,   ordering;
            """;

        yield return
            """
            create schema app;
              create table app.users(
                id bigint not null identity,
              email text not null,
                constraint users_pkey primary key(id));
            --- Backfill the new email column from the legacy table.
            SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$
            UPDATE app.users u SET email = l.email FROM legacy.users l WHERE l.id = u.id;
            $$;
            """;
    }

    // -------------------------------------------------------------------------
    // The trivia ownership rule: trailing trivia runs up to and including the first line break; everything past
    // that leads the next token.
    // -------------------------------------------------------------------------

    private static List<Token> Tokens(string source)
    {
        var lexer = new NsqlLexer(source);
        var tokens = new List<Token>();
        while (true)
        {
            var token = lexer.Next();
            tokens.Add(token);
            if (token.Kind == TokenKind.EndOfFile)
            {
                return tokens;
            }
        }
    }

    [Fact]
    public void Trailing_RunsUpToAndIncludingTheFirstNewline()
    {
        // Arrange
        var tokens = Tokens("a  \nb");

        // Act
        var first = tokens[0];

        // Assert — the whitespace and the newline trail 'a'; 'b' gets no leading trivia.
        first.Text.ShouldBe("a");
        first.Trailing.Select(t => t.Kind).ShouldBe([TriviaKind.Whitespace, TriviaKind.EndOfLine]);
        tokens[1].Leading.ShouldBeEmpty();
    }

    [Fact]
    public void LeadingTakesEverythingPastTheTrailingNewline()
    {
        // Arrange — a blank line sits between the two tokens.
        var tokens = Tokens("a\n\nb");

        // Act
        var second = tokens[1];

        // Assert — 'a' keeps only the first newline; the blank line leads 'b'.
        tokens[0].Trailing.Select(t => t.Kind).ShouldBe([TriviaKind.EndOfLine]);
        second.Text.ShouldBe("b");
        second.Leading.Select(t => t.Kind).ShouldBe([TriviaKind.EndOfLine]);
    }

    [Fact]
    public void TrailingComment_StaysWithItsTokenThroughTheNewline()
    {
        // Arrange
        var tokens = Tokens("users; -- note\ncreate");

        // Act — the ';' token carries the space, comment, and newline as trailing trivia.
        var semicolon = tokens[1];

        // Assert
        semicolon.Kind.ShouldBe(TokenKind.Semicolon);
        semicolon.Trailing.Select(t => t.Kind).ShouldBe(
            [TriviaKind.Whitespace, TriviaKind.LineComment, TriviaKind.EndOfLine]);
        semicolon.Trailing[1].Text.ShouldBe("-- note");
        tokens[2].Text.ShouldBe("create");
    }

    [Fact]
    public void EndOfFile_CarriesTheFilesFinalTriviaAsLeading()
    {
        // Arrange — trailing comment with no final newline.
        var tokens = Tokens("users\n-- trailing");

        // Act
        var eof = tokens[^1];

        // Assert
        eof.Kind.ShouldBe(TokenKind.EndOfFile);
        eof.Leading.ShouldContain(t => t.Kind == TriviaKind.LineComment && t.Text == "-- trailing");
    }

    [Fact]
    public void Raw_KeepsDelimitersWhileTextDecodes()
    {
        // Arrange
        var tokens = Tokens("'it''s'");

        // Act
        var token = tokens[0];

        // Assert — Text is the decoded payload; Raw is the verbatim literal.
        token.Kind.ShouldBe(TokenKind.String);
        token.Text.ShouldBe("it's");
        token.Raw.ShouldBe("'it''s'");
    }
}
