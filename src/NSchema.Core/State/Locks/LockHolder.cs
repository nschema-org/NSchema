using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Serialization;

namespace NSchema.State.Locks;

/// <summary>
/// Who holds a lock, as <c>user@machine</c>.
/// </summary>
[JsonConverter(typeof(ValueObjectJsonConverter))]
public sealed record LockHolder : ValueObject<string>
{
    /// <summary>
    /// Wraps a rendered holder.
    /// </summary>
    public LockHolder(string value) : base(value)
    {
    }

    /// <summary>
    /// The holder for the current process: the current user on the current machine.
    /// </summary>
    public static LockHolder Current() => new($"{Environment.UserName}@{Environment.MachineName}");

    /// <summary>
    /// Wraps a rendered holder. One-way: a holder never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator LockHolder?(string? value) => value is null ? null : new LockHolder(value);
}
