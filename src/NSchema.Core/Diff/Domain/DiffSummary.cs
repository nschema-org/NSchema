namespace NSchema.Diff.Domain;

/// <summary>
/// Aggregate counts of the changes in a <see cref="DatabaseDiff"/>.
/// </summary>
/// <param name="Added">The number of elements being created.</param>
/// <param name="Modified">The number of elements being modified.</param>
/// <param name="Removed">The number of elements being removed.</param>
public sealed record DiffSummary(int Added, int Modified, int Removed);
