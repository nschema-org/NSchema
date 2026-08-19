using NSchema.Model.Columns;

namespace NSchema.Tests.Helpers;

public static class SqlTypeHelpers
{
    /// <summary>
    /// SqlType is a single record (a name plus optional facets), so coverage is by facet shape, not by
    /// an exhaustive list of types: a scalar, the decimal precision/scale pair, fixed and bounded/unbounded
    /// lengths, and a provider-specific name carried through verbatim.
    /// </summary>
    public static TheoryData<SqlType> AllShapes() =>
    [
        SqlType.BigInt,           // scalar, no facets
        SqlType.Decimal(18, 2),   // precision + scale
        SqlType.Char(8),          // fixed length
        SqlType.VarChar(255),     // bounded variable length
        SqlType.VarChar(),        // unbounded variable length
        SqlType.Custom("citext"), // provider-specific name, no facets
    ];
}
