using NSchema.Model.Enums;

namespace NSchema.Diff.Domain.Enums;

/// <summary>
/// Describes one value being added to an enum type, anchored to a neighbouring value so the addition lands at
/// the right position. At most one anchor is set; with neither, the value appends to the end.
/// </summary>
/// <param name="Value">The new enum value.</param>
/// <param name="Before">Add the value before this existing value, when set.</param>
/// <param name="After">Add the value after this value, when set. Additions execute in list order, so the anchor
/// is either pre-existing or was added by an earlier addition in the same diff.</param>
public sealed record EnumValueAddition(EnumLabel Value, EnumLabel? Before = null, EnumLabel? After = null);
