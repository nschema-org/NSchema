using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Helpers;

public static class TestData
{
    public static readonly MigrationAction DestructiveAction = new DropTable("identity", "users");
    public static readonly MigrationAction NonDestructiveAction = new CreateSchema("identity");

    public static readonly DatabaseSchema EmptySchema = DatabaseSchema.Create([]);

    public static readonly MigrationPlan EmptyPlan = new([], EmptySchema);
    public static readonly MigrationPlan DestructivePlan = new([DestructiveAction], EmptySchema);
    public static readonly MigrationPlan NonDestructivePlan = new([NonDestructiveAction], EmptySchema);
}
