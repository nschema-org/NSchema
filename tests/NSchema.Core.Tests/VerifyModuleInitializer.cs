using System.Runtime.CompilerServices;
using NSchema.Model;

namespace NSchema.Tests;

/// <summary>
/// Global Verify configuration.
/// Snapshots live in a <c>Snapshots</c> folder next to the test source file that produced them.
/// </summary>
public static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        DerivePathInfo((sourceFile, _, type, method) => new PathInfo(
            directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name
        ));

        // Gotta ignore these to prevent circular references when creating the snapshot.
        VerifierSettings.IgnoreMember<DatabaseObject>(o => o.Schema);
        VerifierSettings.IgnoreMember<DatabaseMember>(m => m.Parent);
    }
}
