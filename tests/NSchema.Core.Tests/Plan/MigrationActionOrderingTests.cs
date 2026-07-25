using NSchema.Plan.Domain;

namespace NSchema.Tests.Plan;

public sealed class MigrationActionOrderingTests
{
    [Fact]
    public void EveryConcreteActionHasAnOrderingPriority()
    {
        // Arrange
        var actionTypes = typeof(MigrationAction).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(MigrationAction).IsAssignableFrom(type));

        // Act
        var unordered = actionTypes.Where(type => !MigrationActionOrdering.HasPriority(type));

        // Assert
        unordered.ShouldBeEmpty();
    }
}
