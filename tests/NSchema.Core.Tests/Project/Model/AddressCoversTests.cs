using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// Containment runs downward only: an address covers itself and everything beneath it — a schema its objects
/// and members, an object its members — but never its container above, nor a sibling.
/// </summary>
public sealed class AddressCoversTests
{
    private static readonly DatabaseAddress Schema = DatabaseAddress.Schema("app");
    private static readonly ObjectAddress Object = new("app", "orders");
    private static readonly MemberAddress Member = new("app", "orders", "total");

    [Fact]
    public void Schema_CoversItsObjectsAndMembers_AndItself()
    {
        Schema.Covers(Schema).ShouldBeTrue();
        Schema.Covers(Object).ShouldBeTrue();
        Schema.Covers(Member).ShouldBeTrue();
    }

    [Fact]
    public void Schema_DoesNotCoverAnotherSchema()
    {
        Schema.Covers(DatabaseAddress.Schema("billing")).ShouldBeFalse();
        Schema.Covers(new ObjectAddress("billing", "invoices")).ShouldBeFalse();
    }

    [Fact]
    public void Object_CoversItselfAndItsMembers_ButNotItsSchema()
    {
        Object.Covers(Object).ShouldBeTrue();
        Object.Covers(Member).ShouldBeTrue();
        Object.Covers(Schema).ShouldBeFalse();
        Object.Covers(new ObjectAddress("app", "users")).ShouldBeFalse();
    }

    [Fact]
    public void Member_CoversOnlyItself()
    {
        Member.Covers(Member).ShouldBeTrue();
        Member.Covers(Object).ShouldBeFalse();
        Member.Covers(Schema).ShouldBeFalse();
    }

    [Fact]
    public void Covers_IsCaseSensitive()
    {
        Schema.Covers(new ObjectAddress("App", "orders")).ShouldBeFalse();
    }
}
