using System.Diagnostics;

namespace NSchema.Model.Extensions;

/// <summary>
/// Represents a database extension.
/// </summary>
[DebuggerDisplay("{Name,nq} (extension)")]
public sealed class Extension : DatabaseObject, IEquatable<Extension>
{
    /// <inheritdoc/>
    public override DatabaseObjectKind Kind => DatabaseObjectKind.Extension;

    /// <inheritdoc/>
    public override DatabaseAddress Address => DatabaseAddress.Extension(Name);

    /// <summary>
    /// The requested version, or <see langword="null"/> to accept whatever the provider installs.
    /// </summary>
    public string? Version { get; set; }

    /// <inheritdoc/>
    public override Extension Clone() => new() { Name = Name, Version = Version, Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the comment is excluded.
    /// </summary>
    public bool Equals(Extension? other) =>
        other is not null
        && Name == other.Name
        && Version == other.Version;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Extension other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Version);
}
