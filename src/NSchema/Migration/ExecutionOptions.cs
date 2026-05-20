namespace NSchema.Migration;

public record ExecutionOptions(
    DestructiveActionPolicy DestructiveActionPolicy = DestructiveActionPolicy.Error
);
