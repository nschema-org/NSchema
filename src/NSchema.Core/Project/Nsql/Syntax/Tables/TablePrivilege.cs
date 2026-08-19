namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// The grantable table privileges.
/// </summary>
[Flags]
public enum TablePrivilege
{
    /// <summary>
    /// No privilege.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>SELECT</c>.
    /// </summary>
    Select = 1 << 0,

    /// <summary>
    /// <c>INSERT</c>.
    /// </summary>
    Insert = 1 << 1,

    /// <summary>
    /// <c>UPDATE</c>.
    /// </summary>
    Update = 1 << 2,

    /// <summary>
    /// <c>DELETE</c>.
    /// </summary>
    Delete = 1 << 3
}
