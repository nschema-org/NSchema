using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Serialization;

namespace NSchema.State.Locks;

/// <summary>
/// The identity of one held lock: minted at acquisition, and matched exactly to release the right lock.
/// </summary>
[JsonConverter(typeof(ValueObjectJsonConverter))]
public sealed record LockId : ValueObject<string>
{
    /// <summary>
    /// Wraps a rendered lock id.
    /// </summary>
    public LockId(string value) : base(value)
    {
    }

    /// <summary>
    /// Mints a fresh lock id.
    /// </summary>
    public static LockId New() => new(Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Wraps a rendered lock id. One-way: an id never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator LockId?(string? value) => value is null ? null : new LockId(value);
}
