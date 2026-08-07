using Microsoft.Extensions.DependencyInjection;
using NSchema.Commands;
using NSchema.Model;
using NSchema.Project;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Configuration;

/// <summary>
/// Which files the desired schema is read from. An environment overlay is selected by name, not restricted by
/// content, so the schema it declares joins the project alongside the base files' — the same way its configuration
/// statements layer over the base configuration.
/// </summary>
public sealed class DesiredSchemaSourceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-desired-").FullName;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public DesiredSchemaSourceTests() => Directory.SetCurrentDirectory(_root);

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_root, recursive: true);
    }

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_root, name), content);

    private static async ValueTask<ProjectDefinition> Project(params string[] args)
    {
        using var app = CliApplicationBuilder.Create(RootCommand.Create().Parse(args))
            .ConfigureDesiredSchema()
            .Build()
            .Require();

        var project = await app.Services.GetRequiredService<IProjectProvider>()
            .GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        return project.Require();
    }

    private static List<string> TableNames(ProjectDefinition project) =>
        project.Database.Objects<Model.Tables.Table>().Select(t => t.Object.Name.Value).Order().ToList();

    [Fact]
    public async Task DesiredSchema_IncludesTheEnvironmentsTables_WhenAnEnvironmentIsSelected()
    {
        // Arrange
        Write("schema.sql", "CREATE TABLE public.orders (id INT NOT NULL);");
        Write("test.env.test.sql", "CREATE TABLE public.fixtures (id INT NOT NULL);");

        // Act
        var project = await Project("plan", "--environment", "test");

        // Assert
        TableNames(project).ShouldBe(["fixtures", "orders"]);
    }

    [Fact]
    public async Task DesiredSchema_ExcludesEveryEnvironmentsTables_WhenNoEnvironmentIsSelected()
    {
        // Arrange
        Write("schema.sql", "CREATE TABLE public.orders (id INT NOT NULL);");
        Write("test.env.test.sql", "CREATE TABLE public.fixtures (id INT NOT NULL);");

        // Act
        var project = await Project("plan");

        // Assert
        TableNames(project).ShouldBe(["orders"]);
    }

    [Fact]
    public async Task DesiredSchema_ExcludesTheOtherEnvironmentsTables()
    {
        // Arrange
        Write("schema.sql", "CREATE TABLE public.orders (id INT NOT NULL);");
        Write("test.env.test.sql", "CREATE TABLE public.fixtures (id INT NOT NULL);");
        Write("prod.env.prod.sql", "CREATE TABLE public.audit (id INT NOT NULL);");

        // Act
        var project = await Project("plan", "--environment", "test");

        // Assert
        TableNames(project).ShouldBe(["fixtures", "orders"]);
    }

    [Fact]
    public async Task DesiredSchema_ReadsSchemaAndConfigurationFromOneOverlay()
    {
        // Arrange — the overlay mixes both, which is the point: a file's name says when it is read, not what it holds.
        Write("schema.sql", "CREATE TABLE public.orders (id INT NOT NULL);");
        Write("test.env.test.sql", """
            STATE file ( path = 'test.state.json' );

            CREATE TABLE public.fixtures (id INT NOT NULL);
            """);

        // Act
        var project = await Project("plan", "--environment", "test");

        // Assert
        TableNames(project).ShouldBe(["fixtures", "orders"]);
    }

    [Fact]
    public async Task DesiredSchema_ReportsATableTheOverlayRedeclares()
    {
        // Arrange
        Write("schema.sql", "CREATE TABLE public.orders (id INT NOT NULL);");
        Write("test.env.test.sql", "CREATE TABLE public.orders (id INT NOT NULL);");

        // Act
        using var app = CliApplicationBuilder.Create(RootCommand.Create().Parse(["plan", "--environment", "test"]))
            .ConfigureDesiredSchema()
            .Build()
            .Require();

        var project = await app.Services.GetRequiredService<IProjectProvider>()
            .GetProject(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert — an overlay adds to the base, so restating an object collides exactly as two base files would.
        project.ShouldFailContaining("'public.orders' is already declared");
    }
}
