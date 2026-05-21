namespace NSchema.Schema;

[Flags]
public enum TablePrivilege
{
    None = 0,
    Select = 1,
    Insert = 2,
    Update = 4,
    Delete = 8,
}
