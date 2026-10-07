using NSchema.Model;
using NSchema.Model.Publications;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents changing the row changes a publication publishes.
/// </summary>
/// <param name="PublicationName">The name of the publication.</param>
/// <param name="OldOperations">The operations published before the change.</param>
/// <param name="NewOperations">The operations published after the change.</param>
public sealed record SetPublicationOperations(
    SqlIdentifier PublicationName,
    PublishedOperations OldOperations,
    PublishedOperations NewOperations
) : MigrationAction;
