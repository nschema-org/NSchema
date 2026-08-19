using System.ComponentModel.DataAnnotations;
using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A package coordinate: the package that supplies a plugin and the version range to resolve within.
/// </summary>
public sealed record PackageReference
{
    /// <summary>
    /// The package id.
    /// </summary>
    [Required]
    public required PackageId Source { get; init; }

    /// <summary>
    /// The version range to resolve within; its rendered text feeds package resolution.
    /// </summary>
    [Required]
    public required VersionRange Version { get; init; }
}
