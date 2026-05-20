namespace NSchema.Execution;

public record ExecutionOptions(
    DestructiveActionPolicy DestructiveActionPolicy = DestructiveActionPolicy.Error);
