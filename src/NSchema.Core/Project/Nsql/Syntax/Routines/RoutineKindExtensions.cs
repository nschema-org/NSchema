namespace NSchema.Project.Nsql.Syntax.Routines;

/// <summary>
/// Maps the syntax-level routine kind to and from the model's.
/// </summary>
internal static class RoutineKindExtensions
{
    /// <summary>
    /// The model kind this syntax kind projects to.
    /// </summary>
    public static Model.Routines.RoutineKind ToModel(this RoutineKind kind) => kind switch
    {
        RoutineKind.Procedure => Model.Routines.RoutineKind.Procedure,
        RoutineKind.Aggregate => Model.Routines.RoutineKind.Aggregate,
        _ => Model.Routines.RoutineKind.Function,
    };

    /// <summary>
    /// The syntax kind that writes this model kind.
    /// </summary>
    public static RoutineKind ToSyntax(this Model.Routines.RoutineKind kind) => kind switch
    {
        Model.Routines.RoutineKind.Procedure => RoutineKind.Procedure,
        Model.Routines.RoutineKind.Aggregate => RoutineKind.Aggregate,
        _ => RoutineKind.Function,
    };
}
