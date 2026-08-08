using NSchema.Commands;
using NSchema.Commands.Format;

namespace NSchema.Tests.Commands.Format;

public sealed class FormatCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nschema-fmt-" + Guid.NewGuid().ToString("N"));

    public FormatCommandTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private const string Unformatted = "create schema app;\ncreate table app.t(id int not null);\n";
    private const string Formatted = "create schema app;\n\ncreate table app.t (\n  id int not null\n);\n";

    [Fact]
    public void FormatPath_RewritesAnUnformattedFile_AndReturnsIt()
    {
        var file = Write("schema.sql", Unformatted);

        var changed = FormatCommand.FormatPath(_directory, check: false).Require();

        changed.ShouldHaveSingleItem().ShouldBe(file);
        File.ReadAllText(file).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_Check_DoesNotWrite_ButStillReportsTheFile()
    {
        var file = Write("schema.sql", Unformatted);

        var changed = FormatCommand.FormatPath(_directory, check: true).Require();

        changed.ShouldHaveSingleItem().ShouldBe(file);
        File.ReadAllText(file).ShouldBe(Unformatted); // unchanged on disk
    }

    [Fact]
    public void FormatPath_AlreadyFormattedFile_ReportsNoChange()
    {
        Write("schema.sql", Formatted);

        FormatCommand.FormatPath(_directory, check: false).Require().ShouldBeEmpty();
    }

    [Fact]
    public void FormatPath_IsIdempotent()
    {
        var file = Write("schema.sql", Unformatted);

        FormatCommand.FormatPath(_directory, check: false);
        FormatCommand.FormatPath(_directory, check: false).Require().ShouldBeEmpty();
        File.ReadAllText(file).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_RecursesIntoSubdirectories()
    {
        var nested = Write(Path.Combine("app", "tables", "users.sql"), Unformatted);

        FormatCommand.FormatPath(_directory, check: false).Require().ShouldHaveSingleItem().ShouldBe(nested);
        File.ReadAllText(nested).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_OnlyTouchesSqlFiles()
    {
        var ignored = Write("notes.txt", Unformatted);

        FormatCommand.FormatPath(_directory, check: false).Require().ShouldBeEmpty();
        File.ReadAllText(ignored).ShouldBe(Unformatted);
    }

    [Fact]
    public void FormatPath_AcceptsASingleFile()
    {
        var file = Write("schema.sql", Unformatted);

        FormatCommand.FormatPath(file, check: false).Require().ShouldHaveSingleItem().ShouldBe(file);
        File.ReadAllText(file).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_MissingPath_Fails()
        => FormatCommand.FormatPath(Path.Combine(_directory, "nope"), check: false)
            .ShouldFailContaining("No such file or directory");

    [Theory]
    [InlineData("--json")]
    [InlineData("--quiet")]
    [InlineData("--verbose")]
    public void Parse_PresentationFlag_IsAUsageError(string flag)
    {
        // Arrange / Act — format's output is a payload for other tools, so no presentation flag applies to it.
        // Accepting one silently would promise a rendering that never happens.
        var parseResult = RootCommand.Create().Parse(["format", flag, "."]);

        // Assert
        parseResult.Errors.ShouldContain(error => error.Message.Contains($"{flag} cannot be used with 'format'"));
    }

    [Fact]
    public void Parse_Format_IsAUsageError()
    {
        // Act
        var parseResult = RootCommand.Create().Parse(["format", "--format", "markdown", "."]);

        // Assert
        parseResult.Errors.ShouldContain(error => error.Message.Contains("--format cannot be used with 'format'"));
    }

    [Fact]
    public void Parse_WithoutPresentationFlags_IsAccepted()
    {
        // Act — the flags format does honour stay unaffected.
        var parseResult = RootCommand.Create().Parse(["format", "--check", "."]);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
    }
}
