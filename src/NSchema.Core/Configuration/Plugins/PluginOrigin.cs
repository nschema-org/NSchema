namespace NSchema.Configuration.Plugins;

/// <summary>
/// Where a declared plugin comes from.
/// </summary>
/// <remarks>
/// A closed set: the constructor is <see langword="private protected"/>, so only the cases below can derive from it
/// and a consumer's switch can be exhaustive. When the language gains discriminated unions this becomes one.
/// </remarks>
public abstract record PluginOrigin
{
    private protected PluginOrigin()
    {
    }
}
