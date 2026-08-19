using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// An XML index indexes the shredded contents of an XML column. It is written the way SQL Server writes it —
/// the only engine that has them — and standalone rather than inline, because a secondary names the primary it
/// is built over and a table member has no way to refer to another index.
/// </summary>
public sealed class NsqlXmlIndexTests
{
    private static Table ParseTable(string sql) =>
        new TestNsqlParser("CREATE SCHEMA app; CREATE TABLE app.t (id int NOT NULL, doc xml); " + sql)
            .Parse().Database.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();

    private static TableIndex ParseIndex(string sql) => ParseTable(sql).Indexes.ShouldHaveSingleItem();

    [Fact]
    public void Parse_PrimaryXmlIndex_SetsKindAndNamesNoPrimary()
    {
        // Act
        var index = ParseIndex("CREATE PRIMARY XML INDEX pxml ON app.t (doc);");

        // Assert
        index.Name.ShouldBe("pxml");
        index.Xml.ShouldNotBeNull();
        index.Xml!.Kind.ShouldBe(XmlIndexKind.Primary);
        index.Xml.PrimaryIndex.ShouldBeNull();
        index.Columns.ShouldHaveSingleItem().Column!.Value.ShouldBe("doc");
    }

    [Theory]
    [InlineData("PATH", XmlIndexKind.Path)]
    [InlineData("VALUE", XmlIndexKind.Value)]
    [InlineData("PROPERTY", XmlIndexKind.Property)]
    public void Parse_SecondaryXmlIndex_CarriesKindAndItsPrimary(string keyword, XmlIndexKind expected)
    {
        // Act
        var index = ParseIndex($"CREATE XML INDEX sxml ON app.t (doc) USING XML INDEX pxml FOR {keyword};");

        // Assert
        index.Xml!.Kind.ShouldBe(expected);
        index.Xml.PrimaryIndex.ShouldBe("pxml");
    }

    [Fact]
    public void Parse_OrdinaryIndex_HasNoXmlFacet()
        => ParseIndex("CREATE INDEX ix ON app.t (id);").Xml.ShouldBeNull();

    [Fact]
    public void Parse_SecondaryWithoutItsPrimary_FailsTheRead()
        // A secondary indexes a primary's node table, so FOR without USING is not a thing it can mean.
        => Should.Throw<Exception>(() => ParseIndex("CREATE XML INDEX sxml ON app.t (doc) FOR PATH;"));

    [Fact]
    public void Parse_SecondaryWithUnknownKind_FailsTheRead()
        => Should.Throw<Exception>(() => ParseIndex("CREATE XML INDEX sxml ON app.t (doc) USING XML INDEX pxml FOR SPATIAL;"));

    [Fact]
    public void Write_XmlIndexes_EmitStandaloneWithPrimariesFirst()
    {
        // Arrange — declared secondary-first, to prove the order written is the order they must be created in.
        var written = NsqlWriter.Write(Database(
            new TableIndex
            {
                Name = "sxml",
                Columns = ["doc"],
                Xml = new XmlIndexDefinition(XmlIndexKind.Path, "pxml"),
            },
            new TableIndex { Name = "pxml", Columns = ["doc"], Xml = new XmlIndexDefinition(XmlIndexKind.Primary) }));

        // Assert
        written.ShouldContain("CREATE PRIMARY XML INDEX pxml ON app.t(doc);");
        written.ShouldContain("CREATE XML INDEX sxml ON app.t(doc) USING XML INDEX pxml FOR PATH;");
        written.IndexOf("PRIMARY XML INDEX", StringComparison.Ordinal)
            .ShouldBeLessThan(written.IndexOf("USING XML INDEX", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_XmlIndex_IsNotAnInlineTableMember()
        // T-SQL has no inline form, and NSQL's table body cannot forward-reference another index.
        => NsqlWriter.Write(Database(new TableIndex
        {
            Name = "pxml",
            Columns = ["doc"],
            Xml = new XmlIndexDefinition(XmlIndexKind.Primary),
        })).ShouldNotContain("INDEX pxml(doc)");

    [Fact]
    public void Write_XmlIndexes_RoundTripThroughParse()
    {
        var schema = Database(
            new TableIndex { Name = "pxml", Columns = ["doc"], Xml = new XmlIndexDefinition(XmlIndexKind.Primary) },
            new TableIndex { Name = "sxml", Columns = ["doc"], Xml = new XmlIndexDefinition(XmlIndexKind.Value, "pxml") });

        var written = NsqlWriter.Write(schema);
        var table = new TestNsqlParser(written).Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();

        table.Indexes.Count.ShouldBe(2);
        table.Indexes.Single(i => i.Name == "pxml").Xml.ShouldBe(new XmlIndexDefinition(XmlIndexKind.Primary));
        table.Indexes.Single(i => i.Name == "sxml").Xml.ShouldBe(new XmlIndexDefinition(XmlIndexKind.Value, "pxml"));
    }

    [Fact]
    public void Format_WrittenXmlIndexes_AreAlreadyCanonical()
    {
        var written = NsqlWriter.Write(Database(
            new TableIndex { Name = "pxml", Columns = ["doc"], Xml = new XmlIndexDefinition(XmlIndexKind.Primary) },
            new TableIndex { Name = "sxml", Columns = ["doc"], Xml = new XmlIndexDefinition(XmlIndexKind.Property, "pxml") }));

        var result = NsqlWriter.Format(written);

        result.Value.ShouldBe(written);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Write_ViewWhoseBodyStartsWithAComment_KeepsItsIndex()
    {
        // AdventureWorks' vProductAndDescription opens its body with a line comment. The index is written as a
        // statement after the view, so whatever the body does must not swallow it.
        var schema = new Database
        {
            Schemas = [new Schema
            {
                Name = "app",
                Views = [new NSchema.Model.Views.View
                {
                    Name = "v",
                    Body = "-- what it is for.\nSELECT id FROM app.t",
                    IsSchemaBound = true,
                    Indexes = [new TableIndex { Name = "v_ix", Columns = ["id"], IsUnique = true }],
                }],
            }],
        };

        var written = NsqlWriter.Write(schema);
        var view = new TestNsqlParser(written).Parse().Database
            .Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();

        view.Indexes.ShouldHaveSingleItem().Name.ShouldBe("v_ix");
        view.Body.Value.ShouldContain("-- what it is for.");
    }

    private static Database Database(params TableIndex[] indexes) => new()
    {
        Schemas = [new Schema
        {
            Name = "app",
            Tables = [new Table
            {
                Name = "t",
                Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "doc", Type = SqlType.Custom("xml") }],
                Indexes = [.. indexes],
            }],
        }],
    };
}
