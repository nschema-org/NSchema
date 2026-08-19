using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Domain.CompositeTypes;

/// <summary>
/// Represents changing the type of a composite type's field (<c>ALTER TYPE … ALTER ATTRIBUTE … TYPE …</c>).
/// </summary>
/// <param name="Field">The address of the field.</param>
/// <param name="OldType">The field's type before the change.</param>
/// <param name="NewType">The field's type after the change.</param>
public sealed record AlterCompositeFieldType(MemberAddress Field, SqlType OldType, SqlType NewType) : MigrationAction;
