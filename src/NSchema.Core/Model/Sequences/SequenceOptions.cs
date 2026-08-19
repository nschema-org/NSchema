using NSchema.Model.Columns;

namespace NSchema.Model.Sequences;

/// <summary>
/// Represents the options for a sequence. A <see langword="null"/> option means the database
/// provider's default applies.
/// </summary>
/// <param name="DataType">The data type of the values the sequence generates.</param>
/// <param name="StartWith">The initial value of the sequence.</param>
/// <param name="IncrementBy">The value by which the sequence increments.</param>
/// <param name="MinValue">The minimum value the sequence can generate.</param>
/// <param name="MaxValue">The maximum value the sequence can generate.</param>
/// <param name="Cache">The number of values the database preallocates.</param>
/// <param name="Cycle">Whether the sequence wraps around when it reaches its limit.</param>
public sealed record SequenceOptions(
    SqlType? DataType = null,
    long? StartWith = null,
    long? IncrementBy = null,
    long? MinValue = null,
    long? MaxValue = null,
    long? Cache = null,
    bool Cycle = false
);
