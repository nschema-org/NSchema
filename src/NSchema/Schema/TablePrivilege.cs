namespace NSchema.Schema;

/// <summary>
/// Defines the specific privileges that can be granted to a role for a table within the database schema.
/// </summary>
[Flags]
public enum TablePrivilege
{
    /// <summary>
    /// Indicates that no privileges are granted to the role for the table.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the role has the privilege to perform SELECT operations on the table.
    /// </summary>
    Select = 1 << 0,

    /// <summary>
    /// Indicates that the role has the privilege to perform INSERT operations on the table.
    /// </summary>
    Insert = 1 << 1,

    /// <summary>
    /// Indicates that the role has the privilege to perform UPDATE operations on the table.
    /// </summary>
    Update = 1 << 2,

    /// <summary>
    /// Indicates that the role has the privilege to perform DELETE operations on the table.
    /// </summary>
    Delete = 1 << 3,

    /// <summary>
    /// Indicates that the role has the privilege to perform SELECT operations on the table.
    /// </summary>
    ReadOnly = Select,

    /// <summary>
    /// Indicates that the role has the privileges to perform both SELECT and INSERT operations on the table.
    /// </summary>
    AppendOnly = Select | Insert,

    /// <summary>
    /// Indicates that the role has all privileges (SELECT, INSERT, UPDATE, DELETE) on the table.
    /// </summary>
    All = Select | Insert | Update | Delete,
}
