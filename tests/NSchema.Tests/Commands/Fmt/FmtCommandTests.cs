using NSchema.Commands.Fmt;

namespace NSchema.Tests.Commands.Fmt;

public sealed class FmtCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nschema-fmt-" + Guid.NewGuid().ToString("N"));

    public FmtCommandTests() => Directory.CreateDirectory(_directory);

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

        var changed = FmtCommand.FormatPath(_directory, check: false);

        changed.ShouldHaveSingleItem().ShouldBe(file);
        File.ReadAllText(file).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_Check_DoesNotWrite_ButStillReportsTheFile()
    {
        var file = Write("schema.sql", Unformatted);

        var changed = FmtCommand.FormatPath(_directory, check: true);

        changed.ShouldHaveSingleItem().ShouldBe(file);
        File.ReadAllText(file).ShouldBe(Unformatted); // unchanged on disk
    }

    [Fact]
    public void FormatPath_AlreadyFormattedFile_ReportsNoChange()
    {
        Write("schema.sql", Formatted);

        FmtCommand.FormatPath(_directory, check: false).ShouldBeEmpty();
    }

    [Fact]
    public void FormatPath_IsIdempotent()
    {
        var file = Write("schema.sql", Unformatted);

        FmtCommand.FormatPath(_directory, check: false);
        FmtCommand.FormatPath(_directory, check: false).ShouldBeEmpty();
        File.ReadAllText(file).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_RecursesIntoSubdirectories()
    {
        var nested = Write(Path.Combine("app", "tables", "users.sql"), Unformatted);

        FmtCommand.FormatPath(_directory, check: false).ShouldHaveSingleItem().ShouldBe(nested);
        File.ReadAllText(nested).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_OnlyTouchesSqlFiles()
    {
        var ignored = Write("notes.txt", Unformatted);

        FmtCommand.FormatPath(_directory, check: false).ShouldBeEmpty();
        File.ReadAllText(ignored).ShouldBe(Unformatted);
    }

    [Fact]
    public void FormatPath_AcceptsASingleFile()
    {
        var file = Write("schema.sql", Unformatted);

        FmtCommand.FormatPath(file, check: false).ShouldHaveSingleItem().ShouldBe(file);
        File.ReadAllText(file).ShouldBe(Formatted);
    }

    [Fact]
    public void FormatPath_MissingPath_Throws()
        => Should.Throw<FileNotFoundException>(() => FmtCommand.FormatPath(Path.Combine(_directory, "nope"), check: false));
}
