namespace NSchema.Schema;

/// <summary>
/// Represents the options for an identity column in a database schema.
/// </summary>
/// <param name="StartWith">The initial value for the identity column.</param>
/// <param name="MinValue">The minimum value that the identity column can generate.</param>
/// <param name="IncrementBy">The value by which the identity column will increment for each new row.</param>
public record IdentityOptions(long? StartWith, long? MinValue, long? IncrementBy);
