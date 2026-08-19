using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The identifier contract: identity is the exact written text — case-sensitive equality, written-casing
/// preservation, and the collection behaviors (hashing, ordering) that follow from it.
/// </summary>
public class SqlIdentifierTests
{
    [Fact]
    public void Equals_CaseVariants_AreDifferentIdentifiers()
    {
        // Arrange
        SqlIdentifier lower = "users";
        SqlIdentifier mixed = "Users";

        // Assert
        lower.Equals(mixed).ShouldBeFalse();
        (lower == mixed).ShouldBeFalse();
        (lower != mixed).ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameText_AreEqual()
    {
        // Assert
        (new SqlIdentifier("users") == new SqlIdentifier("users")).ShouldBeTrue();
    }

    [Fact]
    public void ImplicitConversion_WrapsTheWrittenText()
    {
        // Arrange
        SqlIdentifier name = "Users";

        // Assert
        name.ShouldBe(new SqlIdentifier("Users"));
    }

    [Fact]
    public void ImplicitConversion_NullString_IsANullIdentifier()
    {
        // Arrange
        string? text = null;

        // Act
        SqlIdentifier? name = text;

        // Assert
        name.ShouldBeNull();
    }

    [Fact]
    public void Equals_DifferentNames_AreNotEqual()
    {
        // Arrange
        SqlIdentifier users = "users";
        SqlIdentifier orders = "orders";

        // Assert
        (users == orders).ShouldBeFalse();
    }

    [Fact]
    public void Value_PreservesWrittenCasing()
    {
        // Arrange
        SqlIdentifier identifier = "MyTable";

        // Assert
        identifier.Value.ShouldBe("MyTable");
        identifier.ToString().ShouldBe("MyTable");
    }

    [Fact]
    public void RecordEquality_NestedIdentifierLists_AreCaseSensitive()
    {
        // Arrange — column lists differing only in case name different columns.
        var current = new PrimaryKey { Name = "pk", ColumnNames = ["ID", "Email"] };
        var desired = new PrimaryKey { Name = "pk", ColumnNames = ["id", "email"] };

        // Assert
        current.Equals(desired).ShouldBeFalse();
        current.Equals(new PrimaryKey { Name = "pk", ColumnNames = ["ID", "Email"] }).ShouldBeTrue();
    }

    [Fact]
    public void HashSet_CaseVariants_AreSeparateEntries()
    {
        // Act
        var set = new HashSet<SqlIdentifier> { "users", "Users", "USERS" };

        // Assert
        set.Count.ShouldBe(3);
    }

    [Fact]
    public void CompareTo_OrdersOrdinally()
    {
        // Arrange
        var names = new List<SqlIdentifier> { "zebra", "Mango", "apple" };

        // Act
        names.Sort();

        // Assert — ordinal order: uppercase sorts before lowercase.
        names.Select(n => n.Value).ShouldBe(["Mango", "apple", "zebra"]);
    }
}
