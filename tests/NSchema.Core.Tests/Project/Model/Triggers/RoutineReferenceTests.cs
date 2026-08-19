using NSchema.Model.Routines;

namespace NSchema.Tests.Project.Model.Triggers;

/// <summary>
/// The as-written reference contract: component-wise identifier equality, and the distinction between
/// a qualified reference and one left to the engine's search path.
/// </summary>
public class RoutineReferenceTests
{
    [Fact]
    public void Equals_CaseVariantComponents_AreDifferentReferences()
    {
        // Arrange
        var lower = new RoutineReference("app", "log_change");
        var mixed = new RoutineReference("App", "LOG_CHANGE");

        // Assert
        lower.ShouldNotBe(mixed);
        lower.ShouldBe(new RoutineReference("app", "log_change"));
    }

    [Fact]
    public void Equals_QualifiedAndUnqualified_AreDistinct()
    {
        // An unqualified reference resolves by search path; it is not the same reference as a qualified one.
        var qualified = new RoutineReference("app", "log");
        var unqualified = new RoutineReference(null, "log");

        // Assert
        qualified.ShouldNotBe(unqualified);
    }

    [Fact]
    public void ToString_RendersAsWritten()
    {
        // Assert
        new RoutineReference("App", "Log").ToString().ShouldBe("App.Log");
        new RoutineReference(null, "log").ToString().ShouldBe("log");
    }

    [Fact]
    public void With_ResolvingAnUnqualifiedReference_SetsTheSchemaPart()
    {
        // The template applicator resolves a template routine into the applied schema by setting the
        // schema part — never by string concatenation.
        var unqualified = new RoutineReference(null, "log");

        // Act
        var qualified = unqualified with { Schema = "app" };

        // Assert
        qualified.ShouldBe(new RoutineReference("app", "log"));
    }
}
