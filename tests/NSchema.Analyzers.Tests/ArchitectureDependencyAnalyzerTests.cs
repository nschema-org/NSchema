namespace NSchema.Analyzers.Tests;

/// <summary>
/// The layering rules, exercised against snippets rather than the engine. A rule that reported nothing would pass
/// every architecture check ever written against it, so each one is shown both holding and firing.
/// </summary>
public sealed class ArchitectureDependencyAnalyzerTests
{
    private readonly ArchitectureDependencyAnalyzer _sut = new();

    [Fact]
    public async Task Reference_WithinOneSlices_IsAllowed()
    {
        // Arrange
        const string source = """
            namespace NSchema.Plan.Domain
            {
                public sealed class Planner
                {
                    public NSchema.Plan.Domain.Action? Next;
                }

                public sealed class Action { }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_ToADeclaredSlice_IsAllowed()
    {
        // Arrange — the table lets Plan see Diff.
        const string source = """
            namespace NSchema.Diff.Domain
            {
                public sealed class DatabaseDiff { }
            }

            namespace NSchema.Plan.Domain
            {
                public sealed class Planner
                {
                    public NSchema.Diff.Domain.DatabaseDiff? Diff;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_ToTheKernel_IsAllowed()
    {
        // Arrange — the kernel is ambient, so no slice declares it.
        const string source = """
            namespace NSchema.Model
            {
                public sealed class Database { }
            }

            namespace NSchema.Apply
            {
                public sealed class SqlExecutor
                {
                    public NSchema.Model.Database? Target;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_ToAnUndeclaredSlice_IsReported()
    {
        // Arrange — Diff may see Project and State, and nothing else outside the kernel.
        const string source = """
            namespace NSchema.Apply
            {
                public interface ISqlExecutor { }
            }

            namespace NSchema.Diff.Domain
            {
                public sealed class DatabaseComparer
                {
                    public NSchema.Apply.ISqlExecutor? Executor;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0001"]);
        diagnostics[0].GetMessage().ShouldBe(
            "'Diff' may not depend on 'NSchema.Apply.ISqlExecutor'. "
            + "Diff may depend only on Project, State, Diagnostics, Extensions, Model.");
    }

    [Fact]
    public async Task Reference_ThroughAStaticCall_IsReportedOnce()
    {
        // Arrange — `Apply.SqlExecutor.Run()` names the type once and its method once; both are the same dependency.
        const string source = """
            namespace NSchema.Apply
            {
                public static class SqlExecutor
                {
                    public static void Run() { }
                }
            }

            namespace NSchema.Diff.Domain
            {
                public sealed class DatabaseComparer
                {
                    public void Compare() => NSchema.Apply.SqlExecutor.Run();
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0001"]);
    }

    [Fact]
    public async Task Reference_FromTheCompositionRoot_IsAllowed()
    {
        // Arrange — composing the slices is what the root is for.
        const string source = """
            namespace NSchema.Operations
            {
                public sealed class PlanOperation { }
            }

            namespace NSchema
            {
                public sealed class NSchemaApplication
                {
                    public NSchema.Operations.PlanOperation? Plan;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_ToTheCompositionRoot_IsAllowedOnlyFromPlugins()
    {
        // Arrange — registering onto the application builder is what a plugin does; nobody else may look up.
        const string source = """
            namespace NSchema
            {
                public sealed class NSchemaApplicationBuilder { }
            }

            namespace NSchema.Plugins
            {
                public interface INSchemaPlugin
                {
                    void Register(NSchema.NSchemaApplicationBuilder builder);
                }
            }

            namespace NSchema.State
            {
                public sealed class DatabaseStateManager
                {
                    public NSchema.NSchemaApplicationBuilder? Builder;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0001"]);
    }

    [Fact]
    public async Task Reference_FromADomainType_ToItsOwnApplicationService_IsReported()
    {
        // Arrange — the table cannot see this one: both sides are the same slice.
        const string source = """
            namespace NSchema.Plan
            {
                public interface IMigrationPlanner { }
            }

            namespace NSchema.Plan.Domain.Services
            {
                public sealed class PlanLinearizer
                {
                    public NSchema.Plan.IMigrationPlanner? Planner;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0002"]);
        diagnostics[0].GetMessage().ShouldBe(
            "'PlanLinearizer' is a domain type and may not depend on the application service "
            + "'NSchema.Plan.IMigrationPlanner'");
    }

    [Fact]
    public async Task Reference_FromADomainType_ToAnotherSlicesApplicationService_IsReported()
    {
        // Arrange — the edge itself is declared, so only the shape rule objects.
        const string source = """
            namespace NSchema.Project
            {
                public interface IProjectProvider { }
            }

            namespace NSchema.Diff.Domain.Services
            {
                public sealed class DatabaseComparer
                {
                    public NSchema.Project.IProjectProvider? Provider;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0002"]);
    }

    [Fact]
    public async Task Reference_FromAProviderSeam_ToOperations_IsReported()
    {
        // Arrange — a seam that sits several folders deep is still a seam.
        const string source = """
            namespace NSchema.Operations
            {
                public sealed class ApplyOperation { }
            }

            namespace NSchema.State.Locks.Plugins
            {
                public interface IStateLock
                {
                    void Observe(NSchema.Operations.ApplyOperation operation);
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBe(["NS0003"]);
        diagnostics[0].GetMessage().ShouldBe(
            "'IStateLock' is a provider seam and may not depend on 'NSchema.Operations.ApplyOperation', "
            + "which belongs to Operations");
    }

    [Fact]
    public async Task Reference_FromConfigurationsPluginSettings_IsNotTreatedAsASeam()
    {
        // Arrange — NSchema.Configuration.Plugins says which plugins a project declares; nobody implements it.
        const string source = """
            namespace NSchema.Project
            {
                public sealed class ProjectDefinition { }
            }

            namespace NSchema.Configuration.Plugins
            {
                public sealed class PluginSettings
                {
                    public NSchema.Project.ProjectDefinition? Project;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_ToAnUndeclaredNamespace_IsLeftToTheNamespaceRule()
    {
        // Arrange — one diagnostic where the slice is introduced beats one per reference to it.
        const string source = """
            namespace NSchema.Mystery
            {
                public sealed class Thing { }
            }

            namespace NSchema.Diff.Domain
            {
                public sealed class DatabaseComparer
                {
                    public NSchema.Mystery.Thing? Thing;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_ToAnotherAssembly_IsIgnored()
    {
        // Arrange — layering is what the engine says about itself, not about what it links against.
        const string provider = """
            namespace NSchema.Apply
            {
                public interface ISqlExecutor { }
            }
            """;

        const string source = """
            namespace NSchema.Diff.Domain
            {
                public sealed class DatabaseComparer
                {
                    public NSchema.Apply.ISqlExecutor? Executor;
                }
            }
            """;

        // Act
        var diagnostics = await Analysis.Run(_sut, source, provider);

        // Assert
        diagnostics.Ids().ShouldBeEmpty();
    }
}
