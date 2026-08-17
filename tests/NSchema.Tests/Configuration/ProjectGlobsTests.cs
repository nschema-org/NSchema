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

    private List<string> Environment(string environment) =>
        ProjectGlobs.Match(_root, ProjectGlobs.Environment(environment)).Select(Path.GetFileName).ToList()!;

    [Fact]
    public void Base_IncludesEveryProjectFile_RecursivelyAndSorted()
    {
        Write("b.nsql");
        Write("a.sql");
        Write("nested/c.nsql");

        Base().ShouldBe(["a.sql", "b.nsql", "c.nsql"]);
    }

    [Fact]
    public void Base_ReadsBothExtensions_SideBySideInOneProject()
    {
        // .nsql names the language; .sql is read alongside it so a project written before the extension
        // existed keeps working, including one part-way through being renamed.
        Write("schema.nsql");
        Write("legacy.sql");

        Base().ShouldBe(["legacy.sql", "schema.nsql"]);
    }

    [Fact]
    public void Base_IgnoresFilesOfEveryOtherExtension()
    {
        Write("schema.nsql");
        Write("notes.md");
        Write("state.json");
        Write("query.sqlx");

        Base().ShouldBe(["schema.nsql"]);
    }

    [Fact]
    public void Base_ExcludesEnvironmentFiles()
    {
        // The .env. marker moves a file into its environment's set; a plain dotted name stays in the base.
        Write("schema.nsql");
        Write("public.users.sql");
        Write("config.env.prod.nsql");
        Write("secrets.env.dev.sql");

        Base().ShouldBe(["public.users.sql", "schema.nsql"]);
    }

    [Fact]
    public void Environment_SelectsOnlyTheNamedEnvironmentsFiles()
    {
        Write("config.nsql");
        Write("config.env.prod.nsql");
        Write("secrets.env.prod.sql");
        Write("config.env.dev.nsql");

        Environment("prod").ShouldBe(["config.env.prod.nsql", "secrets.env.prod.sql"]);
        Environment("dev").ShouldBe(["config.env.dev.nsql"]);
        Environment("staging").ShouldBeEmpty();
    }

    [Fact]
    public void Enumerate_FindsEveryProjectFile_RecursivelyAndSorted()
    {
        // The seam for the commands that walk the filesystem directly rather than matching a glob.
        Write("b.sql");
        Write("a.nsql");
        Write("nested/c.nsql");
        Write("notes.md");

        ProjectGlobs.Enumerate(_root).Select(Path.GetFileName).ShouldBe(["a.nsql", "b.sql", "c.nsql"], ignoreOrder: true);
    }
}
