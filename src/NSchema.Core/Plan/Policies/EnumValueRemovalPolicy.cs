using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that rejects enum value removals and reorders.
/// </summary>
internal sealed class EnumValueRemovalPolicy : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan) => plan.Diff.Schemas
        .SelectMany(schema => schema.Enums)
        .Where(enumDiff => enumDiff.RequiresRecreate)
        .Select(enumDiff => EnumValueRemovalDiagnostics.RequiresRecreate(
            enumDiff.Address,
            enumDiff.Values?.Old,
            enumDiff.Values?.New
        ));
}
