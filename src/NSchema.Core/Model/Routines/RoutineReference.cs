namespace NSchema.Model.Routines;

/// <summary>
/// A reference to a routine as written.
/// </summary>
/// <param name="Schema">The schema qualifier, or <see langword="null"/> when resolution is left to the engine.</param>
/// <param name="Name">The routine's name.</param>
/// <remarks> An unqualified reference is resolved by the engine's search path at execution time.</remarks>
public sealed record RoutineReference(SqlIdentifier? Schema, SqlIdentifier Name)
{

    /// <summary>
    /// The reference as written: <c>schema.name</c>, or the bare name when unqualified.
    /// </summary>
    public override string ToString() => Schema is { } schema ? $"{schema}.{Name}" : Name.Value;
}
