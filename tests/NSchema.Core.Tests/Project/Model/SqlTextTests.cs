using NSchema.Model;

namespace NSchema.Tests.Project.Model;

public sealed class SqlTextTests
{
    [Theory]
    [InlineData("SELECT id FROM app.users", "SELECT  id   FROM\tapp.users")]            // collapsed whitespace
    [InlineData("SELECT id FROM app.users", "SELECT id\n  FROM app.users")]              // newlines + indent
    [InlineData("SELECT id FROM app.users", "  SELECT id FROM app.users  ")]             // leading/trailing trim
    [InlineData("SELECT * FROM app.users", "SELECT * FROM app.users;")]                  // trailing terminator
    [InlineData("SELECT * FROM app.users", "SELECT * FROM app.users ;")]                 // terminator after space
    [InlineData("SELECT 'a  b' AS l", "SELECT  'a  b'  AS  l")]                          // literal kept, gaps collapsed
    public void EquivalentTo_CosmeticDifferences_AreEquivalent(string current, string desired)
        => new SqlText(current).EquivalentTo(new SqlText(desired)).ShouldBeTrue();

    [Theory]
    [InlineData("SELECT 'a  b'", "SELECT 'a b'")]                                        // whitespace inside literal
    [InlineData("SELECT 'It''s' ", "SELECT 'Its'")]                                      // doubled-quote escape differs
    [InlineData("SELECT \"a  b\"", "SELECT \"a b\"")]                                    // quoted identifier preserved
    [InlineData("SELECT id FROM app.users", "SELECT id FROM app.members")]               // genuinely different
    [InlineData("SELECT a;b", "SELECT a")]                                               // interior ';' is significant
    public void EquivalentTo_SignificantDifferences_AreNotEquivalent(string current, string desired)
        => new SqlText(current).EquivalentTo(new SqlText(desired)).ShouldBeFalse();

    [Fact]
    public void EquivalentTo_BodyWithEmbeddedQuotesAndSemicolon()
        => new SqlText("SELECT 'a;b', \"Col\" FROM app.t").EquivalentTo(new SqlText("SELECT 'a;b',  \"Col\"  FROM app.t")).ShouldBeTrue();
}
