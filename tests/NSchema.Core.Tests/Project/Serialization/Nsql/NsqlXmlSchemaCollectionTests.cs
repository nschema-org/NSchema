using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.XmlSchemaCollections;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// An XML schema collection is a named bundle of XSD a typed <c>xml</c> column validates against. Written as
/// SQL Server writes it, the only engine that has them.
/// </summary>
public sealed class NsqlXmlSchemaCollectionTests
{
    private const string Xsd = "'<xsd:schema xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" />'";

    private static Schema ParseSchema(string sql) =>
        new TestNsqlParser("CREATE SCHEMA app; " + sql).Parse().Database.Schemas.ShouldHaveSingleItem();

    [Fact]
    public void Parse_Collection_CarriesItsBody()
    {
        var collection = ParseSchema($"CREATE XML SCHEMA COLLECTION app.survey AS {Xsd};")
            .XmlSchemaCollections.ShouldHaveSingleItem();

        collection.Name.ShouldBe("survey");
        collection.Body.Value.ShouldBe(Xsd);
    }

    [Theory]
    [InlineData("CONTENT", false)]
    [InlineData("DOCUMENT", true)]
    public void Parse_TypedXmlColumn_BindsToItsCollection(string keyword, bool isDocument)
    {
        var column = ParseSchema(
                $"CREATE XML SCHEMA COLLECTION app.survey AS {Xsd}; " +
                $"CREATE TABLE app.t (doc xml({keyword} app.survey));")
            .Tables.ShouldHaveSingleItem().Columns.ShouldHaveSingleItem();

        column.Type.Name.ShouldBe("xml");
        column.Type.Xml.ShouldNotBeNull();
        column.Type.Xml!.IsDocument.ShouldBe(isDocument);
        column.Type.Xml.Collection.Schema.ShouldBe("app");
        column.Type.Xml.Collection.Name.ShouldBe("survey");
    }

    [Fact]
    public void Parse_UntypedXmlColumn_HasNoBinding()
        => ParseSchema("CREATE TABLE app.t (doc xml);")
            .Tables.ShouldHaveSingleItem().Columns.ShouldHaveSingleItem().Type.Xml.ShouldBeNull();

    [Fact]
    public void Write_CollectionAndTypedColumn_RoundTripThroughParse()
    {
        // The collection is written before the table it types, so a read meets each in order.
        var written = NsqlWriter.Write(Database());

        written.ShouldContain("CREATE XML SCHEMA COLLECTION app.survey AS");
        // `xml` is a keyword (XML indexes), so the argument paren takes a space where `varchar(100)` hugs.
        // Cosmetic: it parses and formats the same either way, which is what the round trip below pins.
        written.ShouldContain("doc xml (CONTENT app.survey)");
        written.IndexOf("XML SCHEMA COLLECTION", StringComparison.Ordinal)
            .ShouldBeLessThan(written.IndexOf("CREATE TABLE", StringComparison.Ordinal));

        var schema = new TestNsqlParser(written).Parse().Database.Schemas.ShouldHaveSingleItem();
        schema.XmlSchemaCollections.ShouldHaveSingleItem().Body.Value.ShouldBe(Xsd);
        schema.Tables.ShouldHaveSingleItem().Columns.ShouldHaveSingleItem()
            .Type.Xml!.Collection.Name.ShouldBe("survey");
    }

    [Fact]
    public void Format_WrittenCollection_IsAlreadyCanonical()
    {
        var written = NsqlWriter.Write(Database());
        var result = NsqlWriter.Format(written);

        result.Value.ShouldBe(written);
        result.Warnings.ShouldBeEmpty();
    }

    private static Database Database() => new()
    {
        Schemas = [new Schema
        {
            Name = "app",
            XmlSchemaCollections = [new XmlSchemaCollection { Name = "survey", Body = Xsd }],
            Tables = [new Table
            {
                Name = "t",
                Columns = [new Column
                {
                    Name = "doc",
                    Type = SqlType.Custom("xml") with
                    {
                        Xml = new XmlTypeBinding(new ObjectAddress("app", "survey", SchemaObjectKind.XmlSchemaCollection)),
                    },
                }],
            }],
        }],
    };
}
