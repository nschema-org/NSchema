using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Schemas;
using NSchema.Model.Extensions;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Services;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins the ordering of the root-level extension actions: extensions are created/updated before any schema or
/// object that may depend on them, and dropped only after everything else is gone.
/// </summary>
public sealed class PlanLinearizerExtensionTests
{
    private readonly PlanLinearizer _linearizer = new();

    private static DatabaseDiff Diff(IReadOnlyList<ExtensionDiff> extensions, params SchemaDiff[] schemas) =>
        new(schemas, extensions);

    [Fact]
    public void CreateExtension_IsEmittedBeforeSchemaCreation()
    {
        // Arrange
        var actions = _linearizer.Linearize(Diff(
            [ExtensionDiff.Added(new Extension { Name = "citext" })],
            SchemaDiff.Added("app")), PlanDependencies.None, DialectCapabilities.Standard);

        var createExtension = actions.Select((a, i) => (a, i)).Single(x => x.a is CreateExtension).i;

        // Act
        var createSchema = actions.Select((a, i) => (a, i)).Single(x => x.a is CreateSchema).i;

        // Assert
        createExtension.ShouldBeLessThan(createSchema);
    }

    [Fact]
    public void DropExtension_IsEmittedAfterSchemaDrop()
    {
        // Arrange
        var actions = _linearizer.Linearize(Diff(
            [ExtensionDiff.Removed("citext")],
            SchemaDiff.Removed("app")), PlanDependencies.None, DialectCapabilities.Standard);

        var dropExtension = actions.Select((a, i) => (a, i)).Single(x => x.a is DropExtension).i;

        // Act
        var dropSchema = actions.Select((a, i) => (a, i)).Single(x => x.a is DropSchema).i;

        // Assert
        dropExtension.ShouldBeGreaterThan(dropSchema);
    }

    [Fact]
    public void AddedExtension_WithComment_EmitsCreateThenSetComment()
    {
        var actions = _linearizer.Linearize(Diff(
            [ExtensionDiff.Added(new Extension { Name = "postgis", Comment = "gis" }) with { Comment = new ValueChange<string>(null, "gis") }]), PlanDependencies.None, DialectCapabilities.Standard);

        actions.OfType<CreateExtension>().ShouldHaveSingleItem().Extension.Name.ShouldBe("postgis");
        actions.OfType<SetExtensionComment>().ShouldHaveSingleItem().NewComment.ShouldBe("gis");
    }

    [Fact]
    public void ModifiedExtension_VersionChange_EmitsAlterExtension()
    {
        var actions = _linearizer.Linearize(Diff(
            [ExtensionDiff.Modified("postgis") with { Version = new ValueChange<string>("3.3", "3.4") }]), PlanDependencies.None, DialectCapabilities.Standard);

        var alter = actions.OfType<AlterExtension>().ShouldHaveSingleItem();
        alter.OldVersion.ShouldBe("3.3");
        alter.NewVersion.ShouldBe("3.4");
    }
}
