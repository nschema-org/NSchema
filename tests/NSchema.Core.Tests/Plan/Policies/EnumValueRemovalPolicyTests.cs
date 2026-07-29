using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Schemas;
using NSchema.Model.Enums;
using NSchema.Plan.Policies;

namespace NSchema.Tests.Plan.Policies;

public sealed class EnumValueRemovalPolicyTests
{
    private readonly EnumValueRemovalPolicy _sut = new();

    private static DatabaseDiff DiffWithEnum(EnumDiff enumDiff) =>
        new([SchemaDiff.Containing("app") with { Enums = [enumDiff] }]);

    private static EnumDiff ValueRemoval() => EnumDiff.Modified("app", "status") with
    {
        Values = new ValueChange<IReadOnlyList<EnumLabel>>(["a", "b"], ["a"]),
    };

    [Fact]
    public void Validate_ValueRemoval_IsAnError()
    {
        var diagnostic = _sut.Validate(DiffWithEnum(ValueRemoval())).ShouldHaveSingleItem();

        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Source.ShouldBe("enum-value-removal");
        diagnostic.Message.ShouldContain("app.status");
        diagnostic.Message.ShouldContain("[a, b] -> [a]");
    }

    [Fact]
    public void Validate_ValueAddition_PassesClean()
    {
        var addition = EnumDiff.Modified("app", "status") with
        {
            AddedValues = [new EnumValueAddition("b", After: "a")],
            Values = new ValueChange<IReadOnlyList<EnumLabel>>(["a"], ["a", "b"]),
        };

        _sut.Validate(DiffWithEnum(addition)).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WholeEnumRemoval_IsNotThisPolicysConcern()
        // A whole-enum drop is governed by the (configurable) destructive-action policy instead.
        => _sut.Validate(DiffWithEnum(EnumDiff.Removed("app", "status"))).ShouldBeEmpty();

    [Fact]
    public void Validate_RenameAndCommentOnlyChange_PassesClean()
        => _sut.Validate(DiffWithEnum(EnumDiff.Modified("app", "status") with
        {
            RenamedFrom = "state",
            Comment = new ValueChange<string>("old", "new"),
        })).ShouldBeEmpty();
}
