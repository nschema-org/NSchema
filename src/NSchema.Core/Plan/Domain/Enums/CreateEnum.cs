using NSchema.Model;
using NSchema.Model.Enums;

namespace NSchema.Plan.Domain.Enums;

/// <summary>
/// Represents the creation of an enum type.
/// </summary>
/// <param name="SchemaName">The name of the schema the enum belongs to.</param>
/// <param name="Enum">The definition of the enum type to create.</param>
public sealed record CreateEnum(SqlIdentifier SchemaName, EnumType Enum) : MigrationAction;
