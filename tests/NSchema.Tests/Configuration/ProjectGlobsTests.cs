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

    private List<string> Schema() =>
        ProjectGlobs.Match(_root, ProjectGlobs.Schema()).Select(Path.GetFileName).ToList()!;

    private List<string> BaseConfiguration() =>
        ProjectGlobs.Match(_root, ProjectGlobs.BaseConfiguration()).Select(Path.GetFileName).ToList()!;

    private List<string> EnvironmentConfiguration(string environment) =>
        ProjectGlobs.Match(_root, ProjectGlobs.EnvironmentConfiguration(environment)).Select(Path.GetFileName).ToList()!;

    [Fact]
    public void Schema_IncludesEverySqlFile_RecursivelyAndSorted()
    {
        Write("b.sql");
        Write("a.sql");
        Write("nested/c.sql");

        Schema().ShouldBe(["a.sql", "b.sql", "c.sql"]);
    }

    [Fact]
    public void Schema_ExcludesConfigurationFiles()
    {
        // The .env. marker makes a file configuration; a plain dotted name stays in the schema set.
        Write("schema.sql");
        Write("public.users.sql");
        Write("config.env.sql");
        Write("nested/settings.env.sql");
        Write("config.env.prod.sql");
        Write("secrets.env.dev.sql");

        Schema().ShouldBe(["public.users.sql", "schema.sql"]);
    }

    [Fact]
    public void BaseConfiguration_SelectsEveryBaseConfigFile()
    {
        // Multiple base configuration files may exist; all of them load, for every environment.
        Write("schema.sql");
        Write("config.env.sql");
        Write("nested/state.env.sql");
        Write("config.env.prod.sql");

        BaseConfiguration().ShouldBe(["config.env.sql", "state.env.sql"]);
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
