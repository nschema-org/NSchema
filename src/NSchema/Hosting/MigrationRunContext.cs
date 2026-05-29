using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// Carries a per-run operation override from an explicit <see cref="NSchemaApplication"/> entry point
/// (<c>Plan</c> / <c>Apply</c>) to the host. Mutable, but safe because a host runs once.
/// </summary>
internal sealed class MigrationRunContext
{
    /// <summary>
    /// When set, overrides the configured <see cref="MigrationOptions.Operation"/> for this run.
    /// </summary>
    public MigrationOperation? Override { get; set; }
}
