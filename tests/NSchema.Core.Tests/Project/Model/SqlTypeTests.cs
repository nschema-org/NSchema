using NSchema.Model.Columns;

namespace NSchema.Tests.Project.Model;

public sealed class SqlTypeTests
{
    [Theory]
    [InlineData("citext", "citext")]
    [InlineData("CITEXT", "CITEXT")]
    [InlineData("  CiText  ", "CiText")]
    public void Name_IsTrimmed_AndComparesAsAnIdentifier(string input, string expected)
    {
        // Written casing is preserved; equality is the identifier's, so the exact written name is the identity.
        SqlType.Custom(input).Name.ShouldBe(expected);
    }

    [Fact]
    public void Equality_IgnoresWhitespace_ButNotCase()
    {
        SqlType.Custom(" citext ").ShouldBe(SqlType.Custom("citext"));
        SqlType.Custom("CITEXT").ShouldNotBe(SqlType.Custom("citext"));
    }

    [Fact]
    public void Equality_DistinguishesDifferentNames()
    {
        SqlType.Custom("citext").ShouldNotBe(SqlType.Custom("hstore"));
    }

    [Fact]
    public void ToString_PreservesWrittenCasing()
    {
        SqlType.Custom("  CiText  ").ToString().ShouldBe("CiText");
    }

    [Fact]
    public void Parse_OfNormalizedName_RoundTripsThroughEquality()
    {
        // Act
        var original = SqlType.Custom("CiText");

        // Assert
        SqlType.Parse(original.ToString()).ShouldBe(original);
    }

    [Theory]
    // The DDL is provider-agnostic, so common SQL spellings are accepted as aliases of the canonical type. Aliasing here
    // (rather than preserving them as Custom types) keeps SQL-spelt schemas from drifting against introspection,
    // which always reports the canonical type.
    [InlineData("bool", "boolean")]
    [InlineData("integer", "int")]
    [InlineData("INTEGER", "int")]
    [InlineData("int2", "smallint")]
    [InlineData("int4", "int")]
    [InlineData("int8", "bigint")]
    [InlineData("real", "float")]
    [InlineData("float4", "float")]
    [InlineData("float8", "double")]
    [InlineData("timestamp", "datetime")]
    [InlineData("timestamptz", "datetimeoffset")]
    [InlineData("uuid", "guid")]
    [InlineData("bytea", "varbinary")]
    public void Parse_SqlSpellingAlias_NormalizesToCanonical(string input, string canonical)
    {
        var parsed = SqlType.Parse(input);

        parsed.ShouldBe(SqlType.Parse(canonical));
        parsed.ToString().ShouldBe(canonical);
    }

    [Theory]
    [InlineData("numeric(10, 2)", "decimal(10,2)")]
    [InlineData("character(8)", "char(8)")]
    public void Parse_ParameterisedSqlSpellingAlias_NormalizesToCanonical(string input, string canonical)
    {
        SqlType.Parse(input).ToString().ShouldBe(canonical);
    }

    [Theory]
    [InlineData("varchar(20)", "varchar(10)", TypeConversionRisk.MayFail)]
    [InlineData("bigint", "smallint", TypeConversionRisk.MayFail)]
    [InlineData("int", "bigint", TypeConversionRisk.Safe)]
    [InlineData("citext", "varchar(10)", TypeConversionRisk.Unknown)]
    public void ConversionRiskTo_AssessesKnownTypeConversions(string from, string to, TypeConversionRisk expected)
    {
        SqlType.Parse(from).ConversionRiskTo(SqlType.Parse(to)).ShouldBe(expected);
    }
}
