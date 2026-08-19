using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for <c>CREATE TYPE s.t AS (field &lt;type&gt;, …)</c>.
/// </summary>
public sealed class NsqlParserCompositeTypeTests
{
    private static CompositeType ParseType(string sql) =>
        new TestNsqlParser("CREATE SCHEMA app; " + sql).Parse().Database
            .Schemas.ShouldHaveSingleItem().CompositeTypes.ShouldHaveSingleItem();

    [Fact]
    public void Parse_SimpleType_CapturesNameAndFields()
    {
        // Act
        var type = ParseType("CREATE TYPE app.address AS (street text, zip int);");

        // Assert
        type.Name.ShouldBe("address");
        type.Fields.Count.ShouldBe(2);
        type.Fields[0].Name.ShouldBe("street");
        type.Fields[0].DataType.ShouldBe(SqlType.Text);
        type.Fields[1].Name.ShouldBe("zip");
        type.Fields[1].DataType.ShouldBe(SqlType.Int);
    }

    [Fact]
    public void Parse_SingleField_IsCaptured()
        => ParseType("CREATE TYPE app.point AS (x int);").Fields.ShouldHaveSingleItem().Name.ShouldBe("x");

    [Fact]
    public void Parse_EmptyFieldList_IsCaptured()
        => ParseType("CREATE TYPE app.empty AS ();").Fields.ShouldBeEmpty();

    [Fact]
    public void Parse_QualifiedFieldType_IsCaptured()
        => ParseType("CREATE TYPE app.t AS (id app.typeid);").Fields.ShouldHaveSingleItem()
            .DataType.ShouldBe(SqlType.Custom(new NSchema.Model.SqlIdentifier("app"), "typeid"));

    [Fact]
    public void Parse_RenameType_BecomesADirective()
        => Directives("CREATE SCHEMA app; CREATE TYPE app.address AS (street text); RENAME TYPE app.legacy_address TO address;")
            .ObjectRenames.ShouldHaveSingleItem().To.ShouldBe(new NSchema.Model.SqlIdentifier("address"));

    [Fact]
    public void Parse_WithDocComment_AttachesComment()
        => ParseType("--- a postal address\nCREATE TYPE app.address AS (street text);").Comment.ShouldBe("a postal address");

    [Fact]
    public void Parse_DuplicateType_FailsTheRead()
        => new TestNsqlParser("CREATE SCHEMA app; CREATE TYPE app.t AS (a int); CREATE TYPE app.t AS (b int);").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialType_DoesNotParse()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("PARTIAL TYPE app.t;").Parse())
            .Message.ShouldContain("Unknown statement 'PARTIAL'");

    private static ProjectDirectives Directives(string source)
    {
        var read = NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        return NSchema.Project.ProjectAssembler.Assemble([read.Value]).Value!.Directives;
    }
}
