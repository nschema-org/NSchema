using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Quoted identifiers: quoting carries the characters a bare identifier cannot (and escapes the vocabulary) —
/// it never changes identity, which is the exact written text either way. Both spellings of the delimiters,
/// <c>"…"</c> and <c>[…]</c>, are read the same way.
/// </summary>
public sealed class NsqlQuotedIdentifierTests
{
    [Fact]
    public void Parse_QuotedNames_CarryTheUnquotedText()
    {
        // Arrange
        var project = new TestNsqlParser(
            """"
            CREATE SCHEMA "My Schema";
            CREATE TABLE "My Schema"."Order Details" ("weird ""col""" int NOT NULL);
            """").Parse();

        // Assert
        var schema = project.Database.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("My Schema");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("Order Details");
        table.Columns.ShouldHaveSingleItem().Name.ShouldBe("weird \"col\"");
    }

    [Fact]
    public void Parse_QuotedAndBareSpellings_AreTheSameName()
    {
        // Arrange — quotes are syntax, not identity: "users" and users are the same name.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app."users" (id int NOT NULL);
            """).Parse();

        // Assert
        project.Database.Schemas.Single().Tables.Single().Name.ShouldBe("users");
    }

    [Fact]
    public void Parse_QuotedKeyword_IsAColumnNotAKeyword()
    {
        // Arrange — quoting escapes the vocabulary: "constraint" and "include" are plain columns.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app.t ("constraint" int NOT NULL, "include" int NOT NULL);
            """).Parse();

        // Assert
        project.Database.Schemas.Single().Tables.Single().Columns
            .Select(c => c.Name.Value).ShouldBe(["constraint", "include"]);
    }

    [Fact]
    public void Parse_Unterminated_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE SCHEMA \"app;").Parse())
            .Message.ShouldContain("Unterminated quoted identifier");

    [Fact]
    public void Parse_Empty_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE SCHEMA \"\";").Parse())
            .Message.ShouldContain("cannot be empty");

    [Fact]
    public void Write_QuotesOnlyWhatNeedsIt()
    {
        // Arrange — a name with a space, a name colliding with a member-opening keyword, and a plain one.
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Tables =
                    [
                        new Table
                        {
                            Name = "Order Details",
                            Columns =
                            [
                                new Column { Name = "id", Type = SqlType.Int },
                                new Column { Name = "constraint", Type = SqlType.Int },
                            ],
                        },
                    ],
                },
            ],
        };

        // Act
        var written = NsqlWriter.Write(database);

        // Assert
        written.ShouldContain("CREATE TABLE app.\"Order Details\"");
        written.ShouldContain("\"constraint\" int NOT NULL");
        written.ShouldContain("id int NOT NULL");
    }

    [Fact]
    public void Write_HostileNames_RoundTripThroughTheParser()
    {
        // Arrange — quoting, escaping, keyword collisions: what the writer emits, the parser reads back.
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "My Schema",
                    Tables =
                    [
                        new Table
                        {
                            Name = "weird \"table\"",
                            Columns = [new Column { Name = "include", Type = SqlType.Int }],
                        },
                    ],
                },
            ],
        };

        // Act
        var reparsed = new TestNsqlParser(NsqlWriter.Write(database)).Parse().Database;

        // Assert
        var schema = reparsed.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("My Schema");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("weird \"table\"");
        table.Columns.ShouldHaveSingleItem().Name.ShouldBe("include");
    }

    [Fact]
    public void Format_PreservesQuotedIdentifiers()
    {
        // Arrange
        const string source = "CREATE TABLE app.\"Order Details\" (\"weird \"\"col\"\"\" int NOT NULL);";

        // Act
        var formatted = NsqlWriter.Format(source).Value!;

        // Assert — the formatter emits source verbatim between structural breaks, quotes intact.
        formatted.ShouldContain("\"Order Details\"");
        formatted.ShouldContain("\"weird \"\"col\"\"\"");
    }

    [Fact]
    public void Parse_BracketedNames_CarryTheUnbracketedText()
    {
        // Arrange
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA [My Schema];
            CREATE TABLE [My Schema].[Order Details] ([weird ]]col]]] int NOT NULL);
            """).Parse();

        // Assert
        var schema = project.Database.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("My Schema");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("Order Details");
        table.Columns.ShouldHaveSingleItem().Name.ShouldBe("weird ]col]");
    }

    [Fact]
    public void Parse_BracketedQuotedAndBareSpellings_AreTheSameName()
    {
        // Arrange — the delimiters are syntax, not identity: [users], "users" and users are the same name.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app.[users] (id int NOT NULL);
            CREATE TABLE [app]."orders" (id int NOT NULL);
            """).Parse();

        // Assert
        var schema = project.Database.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("app");
        schema.Tables.Select(t => t.Name.Value).ShouldBe(["users", "orders"]);
    }

    [Fact]
    public void Parse_BracketedKeyword_IsAColumnNotAKeyword()
    {
        // Arrange — bracketing escapes the vocabulary just as quoting does.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app.t ([constraint] int NOT NULL, [include] int NOT NULL);
            """).Parse();

        // Assert
        project.Database.Schemas.Single().Tables.Single().Columns
            .Select(c => c.Name.Value).ShouldBe(["constraint", "include"]);
    }

    [Fact]
    public void Parse_UnterminatedBracket_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE SCHEMA [app;").Parse())
            .Message.ShouldContain("Unterminated bracketed identifier");

    [Fact]
    public void Parse_EmptyBrackets_AreNotAName()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE SCHEMA [];").Parse())
            .Message.ShouldContain("Expected a schema name");

    [Fact]
    public void Parse_ArrayTypeInAnOpaqueExpression_IsUnaffected()
    {
        // Arrange — '[]' in an opaque span stays punctuation, so an array cast still lexes.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app.t (tags text NOT NULL DEFAULT '{}'::text[]);
            """).Parse();

        // Assert
        project.Database.Schemas.Single().Tables.Single().Columns.Single()
            .DefaultExpression.ShouldNotBeNull().Value.ShouldBe("'{}'::text[]");
    }

    [Fact]
    public void Write_BracketedNames_RoundTripAsQuoted()
    {
        // Arrange — brackets are an accepted input spelling; the writer has one output spelling.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA [My Schema];
            CREATE TABLE [My Schema].[Order Details] ([constraint] int NOT NULL);
            """).Parse();

        // Act
        var written = NsqlWriter.Write(project.Database);

        // Assert
        written.ShouldContain("CREATE TABLE \"My Schema\".\"Order Details\"");
        written.ShouldContain("\"constraint\" int NOT NULL");
    }

    [Fact]
    public void Format_PreservesBracketedIdentifiers()
    {
        // Arrange
        const string source = "CREATE TABLE app.[Order Details] ([weird ]]col]]] int NOT NULL);";

        // Act
        var formatted = NsqlWriter.Format(source).Value!;

        // Assert — formatting is a layout pass, so it never respells a name it was given.
        formatted.ShouldContain("[Order Details]");
        formatted.ShouldContain("[weird ]]col]]]");
    }
}
