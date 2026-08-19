namespace NSchema.Diff.Domain;

/// <summary>
/// Represents a change to a single value, capturing its previous and new state.
/// </summary>
/// <typeparam name="T">The type of the value that changed.</typeparam>
/// <param name="Old">The value before the change, or <see langword="null"/> if there was none.</param>
/// <param name="New">The value after the change, or <see langword="null"/> if it was removed.</param>
public sealed record ValueChange<T>(T? Old, T? New);

/// <summary>
/// Factory for <see cref="ValueChange{T}"/>.
/// </summary>
public static class ValueChange
{
    /// <summary>
    /// Returns the change from <paramref name="current"/> to <paramref name="desired"/>, or
    /// <see langword="null"/> when the two are equal (no change to record).
    /// </summary>
    public static ValueChange<T>? Between<T>(T? current, T? desired) where T : class =>
        EqualityComparer<T>.Default.Equals(current, desired) ? null : new ValueChange<T>(current, desired);
}
