using NSchema.Domain.Migration.Actions;

namespace NSchema.Migration;

public sealed class DestructiveActionException(SchemaAction action)
    : Exception($"Destructive action blocked by policy: {action.GetType().Name}")
{
    public SchemaAction Action { get; } = action;
}
