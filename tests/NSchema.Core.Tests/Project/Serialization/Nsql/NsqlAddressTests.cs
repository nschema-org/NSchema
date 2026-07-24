using NSchema.Model;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Reading an address fragment: arity picks the address kind (schema / object / member), quoting carries
/// characters a bare name cannot, and anything else is an error diagnostic rather than an exception.
/// </summary>
public sealed class NsqlAddressTests
{
    [Fact]
    public void ReadAddress_OneSegment_IsASchema()
    {
        // Act
        var result = NsqlReader.ReadAddress("app");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<SchemaAddress>().Schema.ShouldBe("app");
    }

    [Fact]
    public void ReadAddress_TwoSegments_IsAnObject()
    {
        // Act
        var result = NsqlReader.ReadAddress("app.orders");

        // Assert
        var address = result.Value.ShouldBeOfType<ObjectAddress>();
        address.Schema.ShouldBe("app");
        address.Name.ShouldBe("orders");
    }

    [Fact]
    public void ReadAddress_ThreeSegments_IsAMember()
    {
        // Act
        var result = NsqlReader.ReadAddress("app.orders.total");

        // Assert
        var address = result.Value.ShouldBeOfType<MemberAddress>();
        address.Schema.ShouldBe("app");
        address.Object.ShouldBe("orders");
        address.Member.ShouldBe("total");
    }

    [Fact]
    public void ReadAddress_QuotedSegments_CarryTheUnquotedText()
    {
        // Act — quoting lets a segment hold the dots and spaces a bare name cannot.
        var result = NsqlReader.ReadAddress("\"my.schema\".\"table with spaces\"");

        // Assert
        var address = result.Value.ShouldBeOfType<ObjectAddress>();
        address.Schema.ShouldBe("my.schema");
        address.Name.ShouldBe("table with spaces");
    }

    [Fact]
    public void ReadAddress_QuotedAndBareSpellings_AreTheSameAddress()
    {
        // Assert — quotes are syntax, not identity.
        NsqlReader.ReadAddress("app.\"orders\"").Value.ShouldBe(NsqlReader.ReadAddress("app.orders").Value);
    }

    [Fact]
    public void ReadAddress_BracketedSegments_AreTheSameAddress()
    {
        // Assert — an address accepts the bracketed spelling too.
        NsqlReader.ReadAddress("[app].[orders]").Value.ShouldBe(NsqlReader.ReadAddress("app.orders").Value);
    }

    [Fact]
    public void ReadAddress_PreservesCase()
    {
        // Assert
        NsqlReader.ReadAddress("App.Orders").Value.ShouldBeOfType<ObjectAddress>().Name.ShouldBe("Orders");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadAddress_Empty_FailsWithADiagnostic(string source)
    {
        // Act
        var result = NsqlReader.ReadAddress(source);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void ReadAddress_TrailingInput_Fails()
    {
        // Act — the whole fragment must be an address; leftover is rejected, not silently dropped.
        var result = NsqlReader.ReadAddress("app.orders extra");

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ReadAddress_FourSegments_Fails()
    {
        // Act
        var result = NsqlReader.ReadAddress("a.b.c.d");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("three parts");
    }
}
