using NSchema.Model;

namespace NSchema.Plan.Domain.Extensions;

/// <summary>
/// Represents updating an existing extension to a different version.
/// </summary>
/// <param name="ExtensionName">The name of the extension to update.</param>
/// <param name="OldVersion">The extension's version before the update, if known.</param>
/// <param name="NewVersion">The extension's requested version after the update.</param>
public sealed record AlterExtension(SqlIdentifier ExtensionName, string? OldVersion, string? NewVersion) : MigrationAction;
