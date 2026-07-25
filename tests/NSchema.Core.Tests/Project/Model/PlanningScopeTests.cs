using NSchema.Model;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// One axis at two granularities: a schema entry covers the schema and everything inside it; an object entry
/// covers that object alone. An entry covers itself and everything below it — nothing above it.
/// </summary>
public sealed class PlanningScopeTests
{
    private static readonly SqlIdentifier App = new("app");

    private static SchemaAddress Schema(string name) => new(name);

    private static ObjectAddress Address(string schema, string name) => new(schema, name);

    [Fact]
    public void To_Empty_NormalizesToAll()
    {
        PlanningScope.To([]).ShouldBeSameAs(PlanningScope.All);
        PlanningScope.To(Enumerable.Empty<Address>()).ShouldBeSameAs(PlanningScope.All);
    }

    [Fact]
    public void SchemaEntry_CoversTheSchema_AndEverythingInsideIt()
    {
        // Act
        var scope = PlanningScope.To(Schema("app"));

        // Assert
        scope.Contains(App).ShouldBeTrue();
        scope.Contains(Address("app", "users")).ShouldBeTrue();
        scope.Contains(Address("app", "users") with { Kind = ObjectKind.Table }).ShouldBeTrue();
    }

    [Fact]
    public void ObjectEntry_CoversTheObjectAlone_NotItsSchema()
    {
        // Act
        // Membership implies downward through containment, never upward: targeting an object says nothing
        // about its schema's other contents or the schema's own facets.
        var scope = PlanningScope.To([Address("app", "users")]);

        // Assert
        scope.Contains(Address("app", "users")).ShouldBeTrue();
        scope.Contains(App).ShouldBeFalse();
        scope.Contains(Address("app", "orders")).ShouldBeFalse();
    }

    [Fact]
    public void ObjectEntry_IsKindFree()
    {
        // Act
        // A scope entry is an address; which kinds may share a location is engine-specific, so every kind
        // at the address is covered.
        var scope = PlanningScope.To([Address("app", "users")]);

        // Assert
        scope.Contains(Address("app", "users") with { Kind = ObjectKind.Table }).ShouldBeTrue();
        scope.Contains(Address("app", "users") with { Kind = ObjectKind.View }).ShouldBeTrue();
    }

    [Fact]
    public void ObjectEntry_InsideANamedSchema_IsAbsorbed()
    {
        // Act
        var scope = PlanningScope.To([Schema("app"), Address("app", "users"), Address("billing", "orders")]);

        // Assert
        scope.Addresses.ShouldBe([Schema("app"), Address("billing", "orders")]);
    }

    [Fact]
    public void AnyAddress_MakesTheScopeScoped()
    {
        // Act
        // A scope is narrowed by any address, whole-schema or object alike.
        var scope = PlanningScope.To([Schema("app"), Address("billing", "orders")]);

        // Assert
        scope.IsUnscoped.ShouldBeFalse();
        scope.Addresses.ShouldBe([Schema("app"), Address("billing", "orders")]);
    }

    [Fact]
    public void All_IsUnscoped()
    {
        PlanningScope.All.IsUnscoped.ShouldBeTrue();
        PlanningScope.To([Address("app", "users")]).IsUnscoped.ShouldBeFalse();
    }

    [Fact]
    public void Contains_IsCaseSensitive_AtBothGranularities()
    {
        // Act
        var scope = PlanningScope.To([Schema("app"), Address("billing", "orders")]);

        // Assert
        scope.Contains("APP").ShouldBeFalse();
        scope.Contains(Address("Billing", "Orders")).ShouldBeFalse();
        scope.Contains(App).ShouldBeTrue();
        scope.Contains(Address("billing", "orders")).ShouldBeTrue();
    }
}
