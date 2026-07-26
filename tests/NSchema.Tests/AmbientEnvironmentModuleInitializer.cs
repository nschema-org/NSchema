using System.Runtime.CompilerServices;

namespace NSchema.Tests;

/// <summary>
/// Clears every <c>NSCHEMA_*</c> variable before the first test runs.
/// </summary>
/// <remarks>
/// Any setting is overridable from the environment, so a developer with (say) <c>NSCHEMA_DATABASE_CONNECTION_STRING</c>
/// exported would see tests that assert on a written setting fail — the override is working exactly as designed. This
/// makes the assembly's outcome depend on the code under test rather than on the shell that launched it.
/// </remarks>
public static class AmbientEnvironmentModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        foreach (var name in Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(name => name.StartsWith("NSCHEMA_", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }
}
