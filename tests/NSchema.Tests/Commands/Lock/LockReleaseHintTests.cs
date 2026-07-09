using System.CommandLine;
using NSchema.Commands.Lock;

namespace NSchema.Tests.Commands.Lock;

public sealed class LockReleaseHintTests
{
    private readonly RootCommand _root = NSchema.Commands.RootCommand.Create();

    private string Command(string[] args, string? environment) =>
        LockReleaseHint.Command("abc123", environment, _root.Parse(args));

    [Fact]
    public void Command_NoGlobals_IsJustTheReleaseCommand()
        => Command(["lock", "status"], null).ShouldBe("nschema lock release abc123");

    [Fact]
    public void Command_WithEnvironment_CarriesTheEnvironment()
        => Command(["lock", "status", "--environment", "staging"], "staging")
            .ShouldBe("nschema lock release abc123 --environment staging");

    [Fact]
    public void Command_WithDirectory_CarriesTheDirectory()
        => Command(["lock", "status", "--directory", "infra/db"], null)
            .ShouldBe("nschema lock release abc123 --directory infra/db");

    [Fact]
    public void Command_WithBothGlobals_CarriesBoth()
        => Command(["apply", "-e", "staging", "-C", "infra/db"], "staging")
            .ShouldBe("nschema lock release abc123 --environment staging --directory infra/db");

    [Fact]
    public void Command_DirectoryWithSpaces_IsQuoted()
        => Command(["lock", "status", "--directory", "my project/db"], null)
            .ShouldBe("nschema lock release abc123 --directory \"my project/db\"");
}
