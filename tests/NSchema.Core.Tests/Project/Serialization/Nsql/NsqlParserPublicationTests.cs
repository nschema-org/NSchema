using NSchema.Model;
using NSchema.Model.Publications;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for <c>CREATE PUBLICATION</c>, <c>RENAME PUBLICATION</c> and a table's <c>REPLICA IDENTITY</c>.
/// </summary>
public sealed class NsqlParserPublicationTests
{
    private static ProjectDefinition ParseProject(string source) => new TestNsqlParser(source).Parse();

    private static Database Parse(string source) => ParseProject(source).Database;

    [Fact]
    public void Parse_CreatePublication_Bare_PublishesNothingButEveryOperation()
    {
        var publication = Parse("CREATE PUBLICATION feed;").Publications.ShouldHaveSingleItem();

        publication.Name.ShouldBe("feed");
        publication.AllTables.ShouldBeFalse();
        publication.Tables.ShouldBeEmpty();
        publication.Schemas.ShouldBeEmpty();
        publication.Operations.ShouldBe(PublishedOperations.All);
    }

    [Fact]
    public void Parse_CreatePublication_ForAllTables()
        => Parse("CREATE PUBLICATION feed FOR ALL TABLES;").Publications.ShouldHaveSingleItem().AllTables.ShouldBeTrue();

    [Fact]
    public void Parse_CreatePublication_TableRunContinuesWithoutTheKeyword()
    {
        // Act
        var publication = Parse("CREATE PUBLICATION feed FOR TABLE sales.orders, sales.order_lines, TABLES IN SCHEMA audit, archive;")
            .Publications.ShouldHaveSingleItem();

        // Assert
        publication.Tables.Select(t => t.Table.Value).ShouldBe(["sales.orders", "sales.order_lines"]);
        publication.Schemas.ShouldBe([new SqlIdentifier("audit"), new SqlIdentifier("archive")]);
    }

    [Fact]
    public void Parse_CreatePublication_TableWithColumnsAndFilter()
    {
        // Act
        var table = Parse("CREATE PUBLICATION feed FOR TABLE sales.orders(id, status) WHERE (status <> 'draft');")
            .Publications.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();

        // Assert
        table.Columns.ShouldBe([new SqlIdentifier("id"), new SqlIdentifier("status")]);
        table.Filter!.Value.ShouldBe("status <> 'draft'");
    }

    [Fact]
    public void Parse_CreatePublication_PublishListsOperations()
        => Parse("CREATE PUBLICATION feed FOR TABLE sales.orders PUBLISH (INSERT, delete);")
            .Publications.ShouldHaveSingleItem().Operations.ShouldBe(PublishedOperations.Insert | PublishedOperations.Delete);

    [Fact]
    public void Parse_CreatePublication_EmptyPublishListPublishesNothing()
        => Parse("CREATE PUBLICATION feed PUBLISH ();").Publications.ShouldHaveSingleItem().Operations.ShouldBe(PublishedOperations.None);

    [Fact]
    public void Parse_CreatePublication_UnknownOperation_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Parse("CREATE PUBLICATION feed PUBLISH (MERGE);"))
            .Message.ShouldContain("INSERT, UPDATE, DELETE or TRUNCATE");

    [Fact]
    public void Parse_CreatePublication_WithDocComment_AttachesComment()
        => Parse("--- order changes\nCREATE PUBLICATION feed;").Publications.ShouldHaveSingleItem().Comment.ShouldBe("order changes");

    [Fact]
    public void Parse_CreatePublication_InsideTemplate_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Parse("TEMPLATE t BEGIN CREATE PUBLICATION feed; END;"))
            .Message.ShouldContain("database-global");

    [Fact]
    public void Parse_RenamePublication_IsADirective()
    {
        var rename = ParseProject("RENAME PUBLICATION feed TO orders_feed;").Directives.PublicationRenames.ShouldHaveSingleItem();

        rename.From.ShouldBe(DatabaseAddress.Publication("feed"));
        rename.To.ShouldBe(DatabaseAddress.Publication("orders_feed"));
    }

    [Theory]
    [InlineData("REPLICA IDENTITY FULL", ReplicaIdentityKind.Full, null)]
    [InlineData("REPLICA IDENTITY NOTHING", ReplicaIdentityKind.Nothing, null)]
    [InlineData("REPLICA IDENTITY USING INDEX ux_events_key", ReplicaIdentityKind.Index, "ux_events_key")]
    public void Parse_TableReplicaIdentity(string clause, ReplicaIdentityKind kind, string? index)
    {
        // Act
        var table = Parse($"CREATE SCHEMA app; CREATE TABLE app.events (id int) {clause};").Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();

        // Assert
        table.ReplicaIdentity.ShouldBe(new ReplicaIdentity(kind, index));
    }

    [Fact]
    public void Parse_TableWithoutReplicaIdentity_LeavesTheEngineDefault()
        => Parse("CREATE SCHEMA app; CREATE TABLE app.events (id int);").Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem()
            .ReplicaIdentity.ShouldBeNull();

    [Fact]
    public void Write_Publication_RoundTrips()
    {
        // Arrange
        var database = new Database
        {
            Publications =
            [
                new Publication
                {
                    Name = "feed",
                    Tables =
                    [
                        new PublishedTable(new ObjectAddress("sales", "orders"), ["id", "status"], "status <> 'draft'"),
                        new PublishedTable(new ObjectAddress("sales", "order_lines")),
                    ],
                    Schemas = ["audit"],
                    Operations = PublishedOperations.Insert | PublishedOperations.Update,
                    Comment = "order changes",
                },
            ],
        };

        // Act
        var written = NsqlWriter.Write(database);

        // Assert
        written.ShouldContain("CREATE PUBLICATION feed FOR TABLE sales.orders(id, status) WHERE (status <> 'draft'), sales.order_lines, TABLES IN SCHEMA audit PUBLISH (INSERT, UPDATE);");
        Parse(written).Publications.ShouldHaveSingleItem().ShouldBe(database.Publications[0]);
    }

    [Fact]
    public void Write_ReplicaIdentity_RoundTrips()
    {
        // Arrange
        var source = "CREATE SCHEMA app; CREATE TABLE app.events (id int) REPLICA IDENTITY FULL;";

        // Act
        var written = NsqlWriter.Write(Parse(source));

        // Assert
        written.ShouldContain(") REPLICA IDENTITY FULL;");
        Parse(written).Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().ReplicaIdentity.ShouldBe(ReplicaIdentity.Full);
    }
}
