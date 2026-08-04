namespace NSchema.Model.Routines;

/// <summary>
/// The kind of a <see cref="Routine"/>.
/// </summary>
public enum RoutineKind
{
    /// <summary>
    /// A function (has a <c>RETURNS</c> clause; callable in expressions).
    /// </summary>
    Function,

    /// <summary>
    /// A procedure (invoked with <c>CALL</c>).
    /// </summary>
    Procedure,

    /// <summary>
    /// An aggregate function.
    /// </summary>
    Aggregate
}
