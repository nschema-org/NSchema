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

    private List<string> BaseSchema() =>
        ProjectGlobs.Match(_root, ProjectGlobs.BaseSchema()).Select(Path.GetFileName).ToList()!;

    private List<string> EnvironmentSchema(string environment) =>
        ProjectGlobs.Match(_root, ProjectGlobs.EnvironmentSchema(environment)).Select(Path.GetFileName).ToList()!;

    [Fact]
    public void BaseSchema_IncludesEverySqlFile_RecursivelyAndSorted()
    {
        Write("b.sql");
        Write("a.sql");
        Write("nested/c.sql");

        BaseSchema().ShouldBe(["a.sql", "b.sql", "c.sql"]);
    }

    [Fact]
    public void BaseSchema_ExcludesEnvironmentOverlays()
    {
        // Overlay files (and only those) are kept out of the base set; a plain dotted name stays in.
        Write("schema.sql");
        Write("public.users.sql");
        Write("audit.env.prod.sql");
        Write("seed.env.dev.sql");

        BaseSchema().ShouldBe(["public.users.sql", "schema.sql"]);
    }

    [Fact]
    public void EnvironmentSchema_SelectsOnlyTheNamedEnvironmentsOverlays()
    {
        Write("schema.sql");
        Write("audit.env.prod.sql");
        Write("seed.env.dev.sql");
        // An inline deployment script declared in a prod overlay rides along when prod is selected.
        Write("backfill.env.prod.sql");

        EnvironmentSchema("prod").ShouldBe(["audit.env.prod.sql", "backfill.env.prod.sql"]);
        EnvironmentSchema("dev").ShouldBe(["seed.env.dev.sql"]);
        EnvironmentSchema("staging").ShouldBeEmpty();
    }

    [Fact]
    public void EnvironmentNames_ReturnsDistinctSortedNames()
    {
        Write("schema.sql");                 // not an overlay
        Write("public.users.sql");           // dotted, but not an overlay
        Write("audit.env.prod.sql");
        Write("seed.env.prod.sql");          // a second prod overlay — name de-duplicated
        Write("scratch.env.dev.sql");

        ProjectGlobs.EnvironmentNames(_root).ShouldBe(["dev", "prod"]);
    }

    [Fact]
    public void EnvironmentNames_NoOverlays_ReturnsEmpty()
    {
        Write("schema.sql");

        ProjectGlobs.EnvironmentNames(_root).ShouldBeEmpty();
    }
}
