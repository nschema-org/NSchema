using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that rejects enum value removals and reorders. Enum values can only be added, so such a change
/// cannot be planned — the type must be recreated manually. This policy is always-on (not gated by
/// <see cref="DestructiveActionOptions"/>): allowing destructive actions cannot make the change expressible.
/// </summary>
internal sealed class EnumValueRemovalPolicy : IPlanPolicy
{
    private const string PolicyName = "enum-value-removal";

    public IEnumerable<Diagnostic> Validate(MigrationPlan plan) =>
        plan.Diff.Schemas
            .SelectMany(schema => schema.Enums)
            .Where(enumDiff => enumDiff.RequiresRecreate)
            .Select(enumDiff => Diagnostic.Error(PolicyName,
                $"Enum '{enumDiff.Schema}.{enumDiff.Name}' removes or reorders values ([{string.Join(", ", enumDiff.Values!.Old ?? [])}] -> [{string.Join(", ", enumDiff.Values!.New ?? [])}]); enum values can only be added. Recreate the type manually if a removal or reorder is required."));
}
