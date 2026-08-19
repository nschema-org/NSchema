using NSchema.Model;

namespace NSchema.Plan.Domain.Views;

/// <summary>
/// Represents the removal of an existing view from the database schema.
/// </summary>
/// <param name="View">The address of the view.</param>
/// <param name="IsMaterialized">Whether the view being removed is a materialized view.</param>
public sealed record DropView(ObjectAddress View, bool IsMaterialized = false) : MigrationAction;
