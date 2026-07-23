using NSchema.Extensions;
using NSchema.Model;

namespace NSchema.Tests.Extensions;

/// <summary>
/// The <c>--scope</c> values are addresses, not bare schema names: a value names a schema or an object, read
/// under the NSQL identifier rules. Anything the scope model cannot target is reported, never silently dropped.
/// </summary>
public sealed class ScopeExtensionsTests
{
    [Fact]
    public void NoValues_CoversEverything()
    {
        ((string[]?)null).ToPlanningScope().Require().ShouldBeSameAs(PlanningScope.All);
        Array.Empty<string>().ToPlanningScope().Require().ShouldBeSameAs(PlanningScope.All);
    }

    [Fact]
    public void OneSegment_ScopesToTheWholeSchema()
    {
        var scope = new[] { "app" }.ToPlanningScope().Require();

        scope.Addresses.ShouldHaveSingleItem().ShouldBe(new SchemaAddress("app"));
    }

    [Fact]
    public void TwoSegments_ScopesToTheObject()
    {
        var scope = new[] { "app.orders" }.ToPlanningScope().Require();

        scope.Addresses.ShouldHaveSingleItem().ShouldBe(new ObjectAddress("app", "orders"));
    }

    [Fact]
    public void QuotedSegments_CarryDotsAndSpaces()
    {
        // Quoting is what lets a name hold the characters a bare identifier cannot.
        var scope = new[] { "\"my.schema\".\"Order Details\"" }.ToPlanningScope().Require();

        scope.Addresses.ShouldHaveSingleItem().ShouldBe(new ObjectAddress("my.schema", "Order Details"));
    }

    [Fact]
    public void SeveralValues_AreAllCovered()
    {
        var scope = new[] { "app", "billing.invoices" }.ToPlanningScope().Require();

        scope.Addresses.ShouldBe([new SchemaAddress("app"), new ObjectAddress("billing", "invoices")]);
    }

    [Fact]
    public void MemberAddress_IsRejectedAndPointsAtTheObject()
    {
        // A member is a level below what a run can target; the message names the object to use instead.
        var result = new[] { "app.orders.total" }.ToPlanningScope();

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("app.orders");
    }

    [Fact]
    public void UnparsableValue_IsReportedAgainstTheValue()
    {
        var result = new[] { "app..orders" }.ToPlanningScope();

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("app..orders");
    }

    [Fact]
    public void EveryBadValue_IsReported()
    {
        // The CLI is fail-fast but not first-failure: a user fixing their command line sees all of it.
        var result = new[] { "app.orders.total", "billing..invoices" }.ToPlanningScope();

        result.Errors.Count().ShouldBe(2);
    }
}
