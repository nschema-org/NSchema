namespace NSchema.Migration.Execution;

public record ExecutionOptions(
    DestructiveActionPolicy DestructiveActionPolicy = DestructiveActionPolicy.Error
);
