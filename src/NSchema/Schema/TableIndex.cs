namespace NSchema.Schema;

public record TableIndex(string Name, IReadOnlyList<string> ColumnNames, bool IsUnique = false)
{
    public virtual bool Equals(TableIndex? other) =>
        other != null
        && Name == other.Name
        && IsUnique == other.IsUnique
        && ColumnNames.SequenceEqual(other.ColumnNames);

    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames, IsUnique);
}
