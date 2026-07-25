using NSchema.Model;

namespace NSchema.Plan.Domain.Extensions;

/// <summary>
/// Represents the removal of an existing database extension.
/// </summary>
/// <param name="ExtensionName">The name of the extension to remove.</param>
public sealed record DropExtension(SqlIdentifier ExtensionName) : MigrationAction;
