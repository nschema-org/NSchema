namespace NSchema.Model.Services;

/// <summary>
/// One thing in the <see cref="DependencyGraph"/>.
/// </summary>
/// <param name="Address">Where it lives.</param>
/// <param name="Kind">What it is.</param>
internal sealed record DependencyNode(Address Address, DependencyKind Kind)
{
    /// <inheritdoc />
    public override string ToString() => $"{Kind} {Address}";
}
