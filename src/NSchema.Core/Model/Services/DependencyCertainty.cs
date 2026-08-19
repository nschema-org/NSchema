namespace NSchema.Model.Services;

/// <summary>
/// How an edge came to be known.
/// </summary>
/// <remarks>
/// The distinction only matters to a caller that acts on an edge rather than merely orders by it: ordering two
/// things already in a plan costs nothing if the edge is wrong, but pulling something into one does.
/// </remarks>
internal enum DependencyCertainty
{
    /// <summary>
    /// The model states the edge outright, so it is exact.
    /// </summary>
    Stated,

    /// <summary>
    /// NSchema inferred the edge by scanning SQL it does not parse, so it may be wrong in either direction.
    /// </summary>
    Inferred
}
