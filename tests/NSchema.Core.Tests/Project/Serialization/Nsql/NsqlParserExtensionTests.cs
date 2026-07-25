using NSchema.Model;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for the database-global <c>CREATE EXTENSION</c> / <c>DROP EXTENSION</c> statements. Extensions
/// live at the root of the parsed <see cref="Database"/>, not inside a schema.
/// </summary>
public sealed class NsqlParserExtensionTests
{
    private static Database Parse(string source) => new TestNsqlParser(source).Parse().Database;

    [Fact]
    public void Parse_CreateExtension_Bare_RecordsRootLevelExtension()
    {
        var extension = Parse("CREATE EXTENSION citext;").Extensions.ShouldHaveSingleItem();
        extension.Name.ShouldBe("citext");
        extension.Version.ShouldBeNull();
    }

    [Fact]
    public void Parse_CreateExtension_WithVersion_CapturesVersion()
    {
        var extension = Parse("CREATE EXTENSION postgis VERSION '3.4';").Extensions.ShouldHaveSingleItem();
        extension.Name.ShouldBe("postgis");
        extension.Version.ShouldBe("3.4");
    }

    [Fact]
    public void Parse_CreateExtension_QuotedName_AllowsNonIdentifierCharacters()
        // Extension names commonly contain a hyphen (e.g. uuid-ossp), which a bare identifier cannot express.
        => Parse("CREATE EXTENSION 'uuid-ossp';").Extensions.ShouldHaveSingleItem().Name.ShouldBe("uuid-ossp");

    [Fact]
    public void Parse_CreateExtension_WithDocComment_AttachesComment()
        => Parse("--- spatial types\nCREATE EXTENSION postgis;")
            .Extensions.ShouldHaveSingleItem().Comment.ShouldBe("spatial types");

    [Fact]
    public void Parse_CreateExtension_IsNotScopedToASchema()
    {
        // Act
        // An extension declared alongside a schema still lands at the root, not inside the schema.
        var schema = Parse("CREATE SCHEMA app; CREATE EXTENSION citext;");

        // Assert
        schema.Extensions.ShouldHaveSingleItem().Name.ShouldBe("citext");
        schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Parse_DuplicateExtension_FailsTheRead()
        => new TestNsqlParser("CREATE EXTENSION citext; CREATE EXTENSION citext;")
            .Project().Errors.ShouldHaveSingleItem().Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialExtension_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Parse("CREATE PARTIAL EXTENSION citext;"))
            .Message.ShouldContain("after CREATE");

    [Fact]
    public void Parse_CreateExtension_MissingVersionString_Throws()
        => Should.Throw<NsqlSyntaxException>(() => Parse("CREATE EXTENSION postgis VERSION;"))
            .Message.ShouldContain("a version string");

    private static ProjectDirectives Directives(string source)
    {
        var read = NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        return NSchema.Project.ProjectAssembler.Assemble([read.Value]).Value!.Directives;
    }
}
