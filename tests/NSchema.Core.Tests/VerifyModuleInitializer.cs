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
        VerifierSettings.IgnoreMember<SchemaObject>(o => o.Schema);
        VerifierSettings.IgnoreMember<ObjectMember>(m => m.Parent);

        // An object outside a tree has no address and says so by throwing. The serializer reads every getter
        // whether the snapshot needs it or not, so a definition carried by a diff — which records its own
        // location, and holds the definition only to describe shape — would fail the whole snapshot.
        VerifierSettings.IgnoreMembersThatThrow<InvalidOperationException>();
    }
}
