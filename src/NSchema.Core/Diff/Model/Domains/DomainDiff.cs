using NSchema.Diff.Model.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;

namespace NSchema.Diff.Model.Domains;

/// <summary>
/// Describes a change to a domain.
/// </summary>
/// <param name="Schema">The name of the schema the domain belongs to.</param>
/// <param name="Name">The domain name.</param>
/// <param name="Kind">The change to the domain.</param>
/// <param name="RenamedFrom">The previous domain name when the domain is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The desired domain for an add or a base-type recreate; otherwise <see langword="null"/>.</param>
/// <param name="DataType">The change to the base type, set when it changed (which forces a recreate).</param>
/// <param name="Default">The change to the default expression, if any (applied in place with <c>ALTER DOMAIN</c>).</param>
/// <param name="NotNull">The change to the not-null requirement, if any (applied in place with <c>ALTER DOMAIN</c>).</param>
/// <param name="Checks">In-place check-constraint changes (added/dropped via <c>ALTER DOMAIN</c>).</param>
/// <param name="Comment">The change to the domain's comment, if any.</param>
public sealed record DomainDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    SqlIdentifier? RenamedFrom = null,
    DomainType? Definition = null,
    ValueChange<SqlType>? DataType = null,
    ValueChange<SqlDefaultExpression>? Default = null,
    ValueChange<bool>? NotNull = null,
    IReadOnlyList<CheckConstraintDiff>? Checks = null,
    ValueChange<string>? Comment = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// In-place check-constraint changes on the domain (added or dropped); empty when none or when recreating.
    /// </summary>
    public IReadOnlyList<CheckConstraintDiff> Checks { get; init; } = Checks ?? [];

    /// <summary>
    /// The base type changed, so the domain must be dropped and recreated — Postgres has no
    /// <c>ALTER DOMAIN … TYPE</c>. The default, not-null and checks ride along on <see cref="Definition"/>.
    /// </summary>
    public bool RequiresRecreate => DataType is not null;
}
