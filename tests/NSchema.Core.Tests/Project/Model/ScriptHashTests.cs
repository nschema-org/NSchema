using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The script-hash contract: the canonical SHA-256 body hash, normalized to lowercase hex on wrap so
/// equality is exact however a recorded hash was cased.
/// </summary>
public sealed class ScriptHashTests
{
    [Fact]
    public void Compute_IsTheSha256OfTheUtf8Body()
        => ScriptHash.Compute(new SqlText("SELECT 1;")).Value
            .ShouldBe("17db4fd369edb9244b9f91d9aeed145c3d04ad8ba6e95d06247f07a63527d11a");

    [Fact]
    public void Wrap_NormalizesToLowercase()
        => new ScriptHash("ABC123").Value.ShouldBe("abc123");

    [Fact]
    public void Equals_IgnoresTheWrittenCase()
        => new ScriptHash("ABC123").ShouldBe(new ScriptHash("abc123"));

    [Fact]
    public void ImplicitConversion_WrapsARenderedHash()
    {
        // Arrange
        ScriptHash hash = "ABC123";

        // Assert
        hash.ShouldBe(new ScriptHash("abc123"));
    }
}
