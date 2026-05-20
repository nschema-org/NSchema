namespace NSchema.Domain.Schema;

public record PrimaryKey(string Name, IReadOnlyList<string> ColumnNames)
{
    public virtual bool Equals(PrimaryKey? other) =>
        other != null
         && Name == other.Name
         && ColumnNames.SequenceEqual(other.ColumnNames);

    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames);
}
