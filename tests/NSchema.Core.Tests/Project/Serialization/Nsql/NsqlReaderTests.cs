using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Schemas;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The file-aware read seam's contract: results are typed <see cref="NsqlDiagnostic"/>s carrying position
/// (and file, when read from one) structurally, and a failed parse still carries the statements that parsed.
/// </summary>
public sealed class NsqlReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-nsql-reader-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Read_ValidSource_ProducesTheTree()
    {
        // Act
        var result = NsqlReader.Read("CREATE SCHEMA app;");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var statement = result.Value.Statements.ShouldHaveSingleItem().ShouldBeOfType<CreateSchemaStatement>();
        statement.Name.Value.ShouldBe("app");
        result.Value.FilePath.ShouldBeNull();
    }

    [Fact]
    public void Read_SyntaxError_CarriesThePositionStructurally_AndThePartialTree()
    {
        // Act — the first statement parses; the second is broken.
        var result = NsqlReader.Read("CREATE SCHEMA app;\nCREATE SCHEMA 123;");

        // Assert
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Position.Line.ShouldBe(2);
        error.File.ShouldBeNull();
        result.Value!.Statements.ShouldHaveSingleItem().ShouldBeOfType<CreateSchemaStatement>().Name.Value.ShouldBe("app");
    }

    [Fact]
    public void Read_MultipleSyntaxErrors_ReportsThemAll()
    {
        // Arrange
        // Three statements; the first and last are broken, the middle is fine — the parser resyncs at
        // statement boundaries, so both errors surface in one read.
        var result = NsqlReader.Read(
            "CREATE TABLE app.users (id, name text);\n" +
            "CREATE SCHEMA app;\n" +
            "GRANT TRUNCATE ON app.users TO readers;");

        result.IsFailure.ShouldBeTrue();

        // Act
        var errors = result.Errors.ToList();

        // Assert
        errors.Count.ShouldBe(2);
        errors[0].Position.Line.ShouldBe(1);
        errors[1].Position.Line.ShouldBe(3);
    }

    [Fact]
    public void Read_ErrorInsideTemplate_RecoversToTheNextTemplateStatement()
    {
        // Arrange
        // A broken statement inside the body; the template's remaining statement still parses, and the
        // statement after the template is reached.
        var result = NsqlReader.Read(
            "TEMPLATE audit BEGIN\n" +
            "    CREATE TABLE log (id, at timestamptz);\n" +
            "    CREATE TABLE trail (id int);\n" +
            "END;\n" +
            "CREATE SCHEMA app");

        result.IsFailure.ShouldBeTrue();

        // Act
        var errors = result.Errors.ToList();

        // Assert
        errors.Count.ShouldBe(2);
        errors[0].Position.Line.ShouldBe(2);
        errors[1].Position.Line.ShouldBe(5);
    }

    [Fact]
    public async Task ReadFile_StampsThePath_OnTheDocumentAndEveryDiagnostic()
    {
        // Arrange
        var path = Path.Combine(_root, "schema.sql");
        await File.WriteAllTextAsync(path, "CREATE TABLE app.users (", TestContext.Current.CancellationToken);

        // Act
        var result = await NsqlReader.ReadFile(path, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().File.ShouldBe(path);
        result.Value!.FilePath.ShouldBe(path);
    }

    [Fact]
    public async Task ReadFile_MissingFile_FailsWithADiagnostic()
    {
        // Act
        var result = await NsqlReader.ReadFile(Path.Combine(_root, "missing.sql"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        error.File.ShouldBe(Path.Combine(_root, "missing.sql"));
        error.Message.ShouldContain("missing.sql");
    }
}
