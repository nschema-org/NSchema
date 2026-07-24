using NSchema.Configuration;

namespace NSchema.Tests.Configuration;

public sealed class ProjectGlobsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-globs-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "-- placeholder");
    }

    private List<string> Base() =>
        ProjectGlobs.Match(_root, ProjectGlobs.Base()).Select(Path.GetFileName).ToList()!;

    private List<string> EnvironmentConfiguration(string environment) =>
        ProjectGlobs.Match(_root, ProjectGlobs.EnvironmentConfiguration(environment)).Select(Path.GetFileName).ToList()!;

    [Fact]
    public void Base_IncludesEverySqlFile_RecursivelyAndSorted()
    {
        Write("b.sql");
        Write("a.sql");
        Write("nested/c.sql");

        Base().ShouldBe(["a.sql", "b.sql", "c.sql"]);
    }

    [Fact]
    public void Base_ExcludesEnvironmentFiles()
    {
        // The .env. marker makes a file configuration; a plain dotted name stays in the schema set.
        Write("schema.sql");
        Write("public.users.sql");
        Write("config.env.prod.sql");
        Write("secrets.env.dev.sql");

        Base().ShouldBe(["public.users.sql", "schema.sql"]);
    }

    [Fact]
    public void EnvironmentConfiguration_SelectsOnlyTheNamedEnvironmentsFiles()
    {
        Write("config.env.sql");
        Write("config.env.prod.sql");
        Write("secrets.env.prod.sql");
        Write("config.env.dev.sql");

        EnvironmentConfiguration("prod").ShouldBe(["config.env.prod.sql", "secrets.env.prod.sql"]);
        EnvironmentConfiguration("dev").ShouldBe(["config.env.dev.sql"]);
        EnvironmentConfiguration("staging").ShouldBeEmpty();
    }
}
