using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The <see cref="Address"/> contract: the addresses are the containment shapes, they key by value across
/// kinds, and a same-named address at a different containment depth is a different address.
/// </summary>
public class AddressTests
{
    public static TheoryData<Address> Addresses() =>
    [
        DatabaseAddress.Schema("app"),
        DatabaseAddress.Extension("citext"),
        new ObjectAddress("app", "users"),
        new MemberAddress("app", "users", "email"),
    ];

    [Theory]
    [MemberData(nameof(Addresses))]
    public void Address_KeysByValue(Address address)
    {
        // Arrange — an address is a dictionary key (the dependency graph nodes on it), so an equal address
        // must find the entry an identical one stored.
        var keyed = new Dictionary<Address, string> { [address] = "node" };

        // Assert
        keyed.ShouldContainKey(address with { });
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public void ToString_RendersTheAddressValue(Address address)
    {
        // The written form is Value; ToString is sealed onto it so display never drifts from the contract.
        address.ToString().ShouldBe(address.Value);
    }

    [Fact]
    public void Addresses_OfTheSameNameAtDifferentKinds_AreNeverEqual()
    {
        // Arrange — the graph keys every kind of node in one dictionary, so shapes must not collide. A schema
        // and an extension have separate name spaces, so both names can be taken at once.
        var schema = DatabaseAddress.Schema("app");
        var extension = DatabaseAddress.Extension("app");

        // Assert — both render 'app', but they address different things.
        schema.Value.ShouldBe(extension.Value);
        schema.ShouldNotBe(extension);
        schema.Covers(extension).ShouldBeFalse();
    }

    [Fact]
    public void RoutineReference_IsNotAnAddress()
    {
        // A reference as written is not an address: an unqualified one is resolved by the engine's search
        // path, so it identifies nothing on its own.
        typeof(Address).IsAssignableFrom(typeof(RoutineReference)).ShouldBeFalse();
    }
}
