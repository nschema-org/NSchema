namespace NSchema.Model;

/// <summary>
/// A single-value domain primitive: equality is by value, and the value renders as itself.
/// </summary>
public abstract record ValueObject<T>
{
    /// <summary>
    /// Wraps the underlying text.
    /// </summary>
    protected ValueObject(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // ReSharper disable once VirtualMemberCallInConstructor
        Value = Normalize(value);
    }

    /// <summary>
    /// The underlying text.
    /// </summary>
    public T Value { get; }

    /// <inheritdoc />
    public sealed override string ToString() => Value?.ToString() ?? string.Empty;

    /// <summary>
    /// An optional delegate to normalize the incoming value.
    /// </summary>
    /// <param name="value">The incoming value.</param>
    /// <returns></returns>
    protected virtual T Normalize(T value) => value;
}
