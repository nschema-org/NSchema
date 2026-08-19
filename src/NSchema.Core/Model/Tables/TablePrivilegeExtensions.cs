namespace NSchema.Model.Tables;

/// <summary>
/// Decomposition for <see cref="TablePrivilege"/>.
/// </summary>
internal static class TablePrivilegeExtensions
{
    /// <summary>
    /// The individual SQL privileges the flags carry, in canonical order.
    /// </summary>
    public static IEnumerable<string> SqlNames(this TablePrivilege privileges)
    {
        if (privileges.HasFlag(TablePrivilege.Select))
        {
            yield return "SELECT";
        }
        if (privileges.HasFlag(TablePrivilege.Insert))
        {
            yield return "INSERT";
        }
        if (privileges.HasFlag(TablePrivilege.Update))
        {
            yield return "UPDATE";
        }
        if (privileges.HasFlag(TablePrivilege.Delete))
        {
            yield return "DELETE";
        }
    }
}
