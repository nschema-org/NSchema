using System.ComponentModel.DataAnnotations;
using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A single recorded plugin pin from the lockfile: the package (<see cref="Source"/>) and the exact version the
/// project resolved and locked it to.
/// </summary>
public sealed record LockedPlugin
{
    /// <summary>The plugin's NuGet package id.</summary>
    [Required]
    public required PackageId Source { get; init; }

    /// <summary>The exact version the range resolved to.</summary>
    [Required]
    public required SemanticVersion Version { get; init; }
}
