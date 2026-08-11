using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class NsqlFormatterTests
{
    private static string Format(string source) => NsqlWriter.Format(source).Value!;

    // -------------------------------------------------------------------------
    // Views: the attribute clause on a parsed tree and on a synthetic one
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("CREATE VIEW app.v WITH SCHEMABINDING AS SELECT 1;")]
    [InlineData("CREATE MATERIALIZED VIEW app.v WITH SCHEMABINDING AS SELECT 1;")]
    public void Format_SchemaBoundView_IsIdempotent(string statement)
    {
        // A parsed tree carries its own tokens for the clause; formatting twice must reach a fixed point.
        var once = Format("CREATE SCHEMA app;\n\n" + statement);

        once.ShouldContain("WITH SCHEMABINDING AS");
        Format(once).ShouldBe(once);
    }

    [Fact]
    public void Format_SchemaBoundView_KeepsIntraStatementSpacingLikeAnyOtherView()
    {
        // The formatter lays out structure, not inter-token spacing: a one-line statement keeps the spacing it
        // was written with. The attribute clause is no exception, and must behave exactly as MATERIALIZED does.
        const string bound = "CREATE SCHEMA app;\n\nCREATE    VIEW app.v    WITH    SCHEMABINDING    AS SELECT 1;\n";
        const string plain = "CREATE SCHEMA app;\n\nCREATE    MATERIALIZED    VIEW app.v    AS SELECT 1;\n";

        Format(bound).ShouldBe(bound);
        Format(plain).ShouldBe(plain);
    }

    [Fact]
    public void Format_WrittenSchemaBoundView_IsAlreadyCanonical()
    {
        // The synthetic tree the writer builds carries no tokens for the clause — the keywords are supplied by
        // the node itself. What it prints must therefore be what the formatter would print for the same text,
        // or an imported project would never format as a no-op.
        var written = NsqlWriter.Write(new Database
        {
            Schemas = [new Schema
            {
                Name = "app",
                Tables = [new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }] }],
                Views = [new View
                {
                    Name = "v",
                    Body = "SELECT id FROM app.t",
                    IsSchemaBound = true,
                    Indexes = [new TableIndex { Name = "v_ix", Columns = ["id"], IsUnique = true }],
                }],
            }],
        });

        var result = NsqlWriter.Format(written);

        result.Value.ShouldBe(written);
        result.Warnings.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Layout normalisation
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_MessyTable_NormalisesLayoutPreservingContentAndComments()
    {
        const string input =
            """
            create schema app;
            create table app.users(
              id   bigint not null identity,
               email text not null,  -- login
              constraint pk primary key(id))
            ;
            """;

        const string expected =
            """
            create schema app;

            create table app.users (
              id   bigint not null identity,
              email text not null,  -- login
              constraint pk primary key(id)
            );
            """;

        Format(input).ShouldBe(expected + "\n");
    }

    [Fact]
    public void Format_IsIdempotent()
    {
        // Arrange
        const string input =
            """
            create schema app;
            create table app.users(
              id   bigint not null identity,
               email text not null,  -- login
              constraint pk primary key(id))
            ;
            """;

        // Act
        var once = Format(input);

        // Assert
        Format(once).ShouldBe(once);
    }

    [Fact]
    public void Format_MessyTemplate_NormalisesLayout()
    {
        const string input =
            """
            template outbox begin
            create table outbox(
              id   uuid not null,
                payload text not null);
                  create index ix_outbox on outbox(id);
              end;
            APPLY TEMPLATE outbox IN SCHEMA billing,  ordering;
            """;

        const string expected =
            """
            template outbox
            BEGIN
              create table outbox (
                id   uuid not null,
                payload text not null
              );

              create index ix_outbox on outbox(id);
            END;

            APPLY TEMPLATE outbox IN SCHEMA billing,  ordering;
            """;

        Format(input).ShouldBe(expected + "\n");
    }

    [Fact]
    public void Format_Template_PreservesCommentsInsideTheBody()
        => Format(
            """
            TEMPLATE t
            BEGIN
              -- the outbox itself
              CREATE TABLE outbox (id int NOT NULL);
            END;
            """).ShouldContain("-- the outbox itself");

    [Fact]
    public void Format_Template_IsIdempotent()
    {
        // Arrange
        const string input =
            """
            template outbox begin
            create table outbox(
              id   uuid not null);
                  create index ix_outbox on outbox(id);
              end;
            """;

        // Act
        var once = Format(input);

        // Assert
        Format(once).ShouldBe(once);
    }

    [Fact]
    public void Format_EmptyTemplate_KeepsBeginAndEndAdjacent()
        => Format("TEMPLATE t BEGIN END;").ShouldBe("TEMPLATE t\nBEGIN\nEND;\n");

    [Fact]
    public void Format_MessyTableTemplate_BreaksOneMemberPerLine()
    {
        const string input =
            """
            template audit_columns for table begin
              created_at datetimeoffset not null,
                updated_at datetimeoffset not null,  -- touched on write
             constraint chk_audit check (updated_at >= created_at)
              end;
            """;

        const string expected =
            """
            template audit_columns for table
            BEGIN
              created_at datetimeoffset not null,
              updated_at datetimeoffset not null,  -- touched on write
              constraint chk_audit check (updated_at >= created_at)
            END;
            """;

        Format(input).ShouldBe(expected + "\n");
    }

    [Fact]
    public void Format_TableTemplate_IsIdempotent()
    {
        // Arrange
        const string input =
            """
            template audit_columns for table begin
              created_at datetimeoffset not null,
                updated_at datetimeoffset not null
              end;
            """;

        // Act
        var once = Format(input);

        // Assert
        Format(once).ShouldBe(once);
    }

    [Fact]
    public void Format_TableWithInclude_KeepsTheIncludeMember()
        => Format("create table app.t (id uuid not null, INCLUDE audit_columns);")
            .ShouldContain("  INCLUDE audit_columns\n");

    [Fact]
    public void Format_PreservesKeywordCasingAndExpressionSpelling()
    {
        // Act
        // Gentle: content is preserved verbatim (lowercase keywords, the '> 0' spacing inside the CHECK).
        const string input = "create table app.t (\n  qty int not null,\n  constraint c check (qty > 0)\n);";

        // Assert
        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_SeparatesTopLevelStatementsWithOneBlankLine()
        => Format("create schema a;\ncreate schema b;\ncreate schema c;")
            .ShouldBe("create schema a;\n\ncreate schema b;\n\ncreate schema c;\n");

    [Fact]
    public void Format_SchemaGrant_IsSeparatedFromTheSchemaDeclaration()
        => Format("CREATE SCHEMA claims;\nGRANT USAGE ON SCHEMA claims TO api;")
            .ShouldBe("CREATE SCHEMA claims;\n\nGRANT USAGE ON SCHEMA claims TO api;\n");

    [Fact]
    public void Format_TableGrant_IsSeparatedFromTheTableDeclaration()
        => Format("CREATE TABLE app.t (id int NOT NULL);\nGRANT SELECT ON app.t TO api;")
            .ShouldBe("CREATE TABLE app.t (\n  id int NOT NULL\n);\n\nGRANT SELECT ON app.t TO api;\n");

    // -------------------------------------------------------------------------
    // Verbatim (non-breaking) statements keep their bodies
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_MultiLineViewBody_IsPreservedVerbatim()
    {
        const string input =
            """
            create view app.active as
              select id, name
              from app.users
              where active;
            """;

        // A view body never breaks like a table; it is emitted as-is (only the leading indentation is trimmed).
        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_DeploymentScript_KeepsDollarBodyAndInternalSemicolons()
    {
        const string input =
            """
            SCRIPT seed RUN ON PRE DEPLOYMENT AS $$
            INSERT INTO app.t VALUES (1);
            INSERT INTO app.t VALUES (2);
            $$;
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_Migration_StartsANewStatement()
    {
        // SCRIPT is a statement keyword: it must break away from the preceding statement with one blank line
        // (rather than being mistaken for a settings statement or trailing content).
        const string input =
            """
            create schema app;
            SCRIPT backfill RUN ON ADD COLUMN app.users.email AS $$
            UPDATE app.users SET email = '';
            $$;
            """;

        const string expected =
            """
            create schema app;

            SCRIPT backfill RUN ON ADD COLUMN app.users.email AS $$
            UPDATE app.users SET email = '';
            $$;
            """;

        Format(input).ShouldBe(expected + "\n");
    }

    [Fact]
    public void Format_MigrationInsideTemplate_IsIdempotent()
    {
        // Arrange
        const string input =
            """
            template outbox begin
            create table outbox_events(id int not null, trace_id text not null);
             SCRIPT backfill run on add column outbox_events.trace_id as $$
            UPDATE {schema}.outbox_events SET trace_id = '';
              $$;
            end;
            """;

        // Act
        var once = Format(input);

        // Assert
        Format(once).ShouldBe(once);
        once.ShouldContain("SCRIPT backfill");
    }

    [Fact]
    public void Format_Migration_IsIdempotent()
    {
        // Arrange
        const string input =
            """
            create schema app;
            SCRIPT noop_ids RUN ON ALTER COLUMN TYPE app.users.id (run_outside_transaction = true) AS $$
            UPDATE app.users SET id = id;
            $$;
            """;

        // Act
        var once = Format(input);

        // Assert
        Format(once).ShouldBe(once);
    }

    [Fact]
    public void Format_EnumStaysInline()
        => Format("create enum app.status ('pending', 'shipped');")
            .ShouldBe("create enum app.status ('pending', 'shipped');\n");

    // -------------------------------------------------------------------------
    // Configuration blocks break their attribute list
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_SettingsStatement_BreaksSettingsOnePerLine()
        => Format("DATABASE ( dialect = postgres, colour = false );")
            .ShouldBe("DATABASE (\n  dialect = postgres,\n  colour = false\n);\n");

    // -------------------------------------------------------------------------
    // Comments
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_LeadingComments_HugTheFollowingStatement()
    {
        const string input = "-- a plain note\n--- a doc comment\ncreate schema app;";
        Format(input).ShouldBe("-- a plain note\n--- a doc comment\ncreate schema app;\n");
    }

    [Fact]
    public void Format_BlankLineBetweenLeadingCommentAndStatement_IsPreserved()
    {
        // A standalone header comment the author separated with a blank line keeps that blank, rather than being
        // force-hugged onto the statement below.
        const string input =
            """
            -- a file header that stands on its own
            -- spanning two lines

            create schema app;
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_MultipleBlankLinesBeforeStatement_CollapseToOne()
        => Format("-- header\n\n\n\ncreate schema app;").ShouldBe("-- header\n\ncreate schema app;\n");

    [Fact]
    public void Format_BlankLineBetweenLeadingComments_IsPreserved()
    {
        const string input =
            """
            -- a section heading

            -- a note about the statement
            create schema app;
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_TrailingLineCommentOnStatement_StaysOnItsLine()
        => Format("create schema app; -- the app schema")
            .ShouldBe("create schema app;  -- the app schema\n");

    [Fact]
    public void Format_LineCommentBeforeMemberComma_PutsCommaBeforeTheComment()
    {
        // Act
        // The ',' must land before the line comment, never inside it (which would comment the ',' out).
        const string input = "create table app.t (\n  a int -- first\n, b int\n);";

        // Assert
        Format(input).ShouldBe("create table app.t (\n  a int,  -- first\n  b int\n);\n");
    }

    [Fact]
    public void Format_OwnLineCommentBetweenMembers_IndentsWithTheMembers()
    {
        // Act
        const string input = "create table app.t (\n  a int,\n  -- the b column\n  b int\n);";

        // Assert
        Format(input).ShouldBe("create table app.t (\n  a int,\n  -- the b column\n  b int\n);\n");
    }

    [Fact]
    public void Format_MultiLineDocCommentOnMember_IndentsEveryLine()
    {
        // A multi-line doc-comment is one leading item spanning lines, so every line takes the member indent.
        const string input =
            """
            create table app.t (
              --- First line comment.
              --- Second line comment.
              id text NOT NULL
            );
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_MultiLineDocCommentInsideTemplate_IndentsEveryLine()
    {
        const string input =
            """
            TEMPLATE my_template
            BEGIN
              CREATE TABLE dummy (
                --- First line comment.
                --- Second line comment.
                id text NOT NULL
              );
            END;
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_MultipleCommentsAfterLastMember_KeepEachOnItsOwnLine()
    {
        // Regression: dangling comments after the final member must not be flattened onto one line.
        const string input =
            """
            DATABASE postgres (
              connection_string = ''
              -- credentials may come from the environment
              -- and override the connection string
            );
            """;

        Format(input).ShouldBe(input + "\n");
    }

    [Fact]
    public void Format_TrailingCommentOnLastMember_StaysInline()
    {
        // A genuinely same-line comment on the last member stays inline (the fix must not move it to its own line).
        const string input =
            """
            STATE file (
              path = './state.json'  -- where state lives
            );
            """;

        Format(input).ShouldBe(input + "\n");
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_EmptyInput_IsEmpty() => Format("").ShouldBe("");

    [Fact]
    public void Format_WhitespaceOnly_IsEmpty() => Format("   \n\n  ").ShouldBe("");

    [Fact]
    public void Format_CommentsOnly_ArePreserved()
        => Format("-- just a note\n").ShouldBe("-- just a note\n");

    [Fact]
    public void Format_Null_Throws()
        => Should.Throw<ArgumentNullException>(() => Format(null!));

    // -------------------------------------------------------------------------
    // Rich snapshot — a deliberately messy document across many constructs
    // -------------------------------------------------------------------------

    [Fact]
    public Task Format_RichDocument_MatchesSnapshot()
    {
        const string input =
            """
            DATABASE (   dialect = postgres   );

            create schema app;
            grant usage on schema app to readonly;

            --- The order status.
            create enum app.status ('pending','shipped','delivered');

            create sequence app.order_seq (start 100, increment 1);

            create table app.orders(
                id bigint not null identity,
                  status app.status not null default 'pending',  -- current state
                total numeric not null,
                -- the audit columns
                created_at timestamptz not null default now(),
                constraint orders_pkey primary key (id),
                constraint total_positive check (total > 0))
            ;

            create view app.open_orders as
                select id, total
                from app.orders
                where status = 'pending';

            SCRIPT reindex RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$
            REINDEX TABLE app.orders;
            $$;
            """;

        return Verify(Format(input));
    }

    [Fact]
    public void Format_RichDocument_IsIdempotent()
    {
        // Arrange
        const string input =
            """
            DATABASE (   dialect = postgres   );

            create schema app;
            grant usage on schema app to readonly;

            --- The order status.
            create enum app.status ('pending','shipped','delivered');

            create sequence app.order_seq (start 100, increment 1);

            create table app.orders(
                id bigint not null identity,
                  status app.status not null default 'pending',  -- current state
                total numeric not null,
                -- the audit columns
                created_at timestamptz not null default now(),
                constraint orders_pkey primary key (id),
                constraint total_positive check (total > 0))
            ;

            create view app.open_orders as
                select id, total
                from app.orders
                where status = 'pending';

            SCRIPT reindex RUN ON POST DEPLOYMENT (run_outside_transaction = true) AS $$
            REINDEX TABLE app.orders;
            $$;
            """;

        // Act
        var once = Format(input);

        // Assert
        Format(once).ShouldBe(once);
    }

    // -------------------------------------------------------------------------
    // Diagnostics (fmt --check)
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_AlreadyCanonical_ReportsNoViolations()
    {
        // Arrange
        const string source =
            """
            create schema app;

            create table app.users (
              id bigint not null
            );
            """;

        // Act
        var result = NsqlWriter.Format(source + "\n");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Format_NonCanonicalStatements_ReportsOneViolationEach()
    {
        // Arrange — a canonical schema, then a table needing reflow, then one preceded by too many blank lines.
        const string source =
            """
            create schema app;
            create table app.users(id bigint not null);



            create schema audit;
            """;

        // Act
        var result = NsqlWriter.Format(source);

        // Assert — the messy table (blank-line gap and reflow) and the over-spaced schema; the first is clean.
        result.Warnings.Select(w => w.Position.Line).ShouldBe([2, 6]);
        result.Warnings.ShouldAllBe(w => w.Source == "formatting" && w.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Format_SyntaxError_RidesAsAnErrorButStillFormats()
    {
        // Arrange — a run condition on a change event is a syntax error, but the node still round-trips.
        const string source = "SCRIPT x RUN ONCE ON ADD COLUMN app.users.email AS $$ SELECT 1; $$;";

        // Act
        var result = NsqlWriter.Format(source);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(d => d.Source == "syntax");
        result.Value.ShouldNotBeNull();
    }
}
