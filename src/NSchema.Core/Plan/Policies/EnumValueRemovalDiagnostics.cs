using NSchema.Model;
using NSchema.Model.Enums;

namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="EnumValueRemovalPolicy"/>.
/// </summary>
internal static class EnumValueRemovalDiagnostics
{
    internal static readonly DiagnosticSource Source = "enum-value-removal";

    /// <summary>
    /// An enum change that removes or reorders values, which cannot be planned.
    /// </summary>
    public static Diagnostic RequiresRecreate(ObjectAddress enumType, IEnumerable<EnumLabel>? oldValues, IEnumerable<EnumLabel>? newValues) =>
        Diagnostic.Error(Source, "requires-recreate", $"Enum '{enumType}' removes or reorders values ([{string.Join(", ", oldValues ?? [])}] -> [{string.Join(", ", newValues ?? [])}]), but enum values can only be added.");
}
