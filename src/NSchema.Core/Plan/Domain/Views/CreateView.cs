using NSchema.Model;
using NSchema.Model.Views;

namespace NSchema.Plan.Domain.Views;

/// <summary>
/// Represents the creation of a view that does not yet exist.
/// </summary>
/// <param name="SchemaName">The name of the schema the view belongs to.</param>
/// <param name="View">The definition of the view to create.</param>
public sealed record CreateView(SqlIdentifier SchemaName, View View) : MigrationAction;
