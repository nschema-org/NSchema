using NSchema.Model;

using NSchema.Model.Enums;

namespace NSchema.Plan.Domain.Enums;

/// <summary>
/// Represents adding one value to an existing enum type, optionally anchored to a neighbouring value. At most
/// one anchor is set; with neither, the value appends to the end.
/// </summary>
/// <param name="Enum">The address of the enum.</param>
/// <param name="Value">The value to add.</param>
/// <param name="Before">Add the value before this existing value, when set.</param>
/// <param name="After">Add the value after this existing value, when set.</param>
public sealed record AddEnumValue(
    ObjectAddress Enum,
    EnumLabel Value,
    EnumLabel? Before = null,
    EnumLabel? After = null
) : MigrationAction;
