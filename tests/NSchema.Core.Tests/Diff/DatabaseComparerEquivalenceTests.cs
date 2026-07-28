using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

/// <summary>
/// Pins the comparer to the registered <see cref="SqlEquivalence"/>: the dialect-aware rule decides whether
/// a type or default changed, so neither side's spelling is the sanctioned one.
/// </summary>
public sealed class DatabaseComparerEquivalenceTests
{
    [Fact]
    public void Compare_CastSpelledDefault_NeutralEquivalence_ReportsChange()
    {
        var column = DiffColumn(new SqlEquivalence(),
            new Column { Name = "status", Type = SqlType.Text, DefaultExpression = "'a'::text" },
            new Column { Name = "status", Type = SqlType.Text, DefaultExpression = "'a'" });

        column.ShouldNotBeNull().Default.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_CastSpelledDefault_DialectEquivalence_ReportsNoChange()
    {
        var column = DiffColumn(new CastFoldingEquivalence(),
            new Column { Name = "status", Type = SqlType.Text, DefaultExpression = "'a'::text" },
            new Column { Name = "status", Type = SqlType.Text, DefaultExpression = "'a'" });

        column.ShouldBeNull();
    }

    [Fact]
    public void Compare_QualifiedCustomType_NeutralEquivalence_ReportsChange()
    {
        var column = DiffColumn(new SqlEquivalence(),
            new Column { Name = "payload", Type = SqlType.Custom("pg_catalog", "jsonb") },
            new Column { Name = "payload", Type = SqlType.Custom("jsonb") });

        column.ShouldNotBeNull().Type.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_QualifiedCustomType_DialectEquivalence_ReportsNoChange()
    {
        var column = DiffColumn(new CastFoldingEquivalence(),
            new Column { Name = "payload", Type = SqlType.Custom("pg_catalog", "jsonb") },
            new Column { Name = "payload", Type = SqlType.Custom("jsonb") });

        column.ShouldBeNull();
    }

    [Fact]
    public void Compare_DomainDefault_RoutesThroughEquivalence()
    {
        var diff = DiffSchemas(new CastFoldingEquivalence(),
            new Schema { Name = "app", Domains = [new DomainType { Name = "d", DataType = SqlType.Text, Default = "'a'::text" }] },
            new Schema { Name = "app", Domains = [new DomainType { Name = "d", DataType = SqlType.Text, Default = "'a'" }] });

        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_CompositeFieldType_RoutesThroughEquivalence()
    {
        var diff = DiffSchemas(new CastFoldingEquivalence(),
            new Schema { Name = "app", CompositeTypes = [new CompositeType { Name = "c", Fields = [new CompositeField("f", SqlType.Custom("pg_catalog", "jsonb"))] }] },
            new Schema { Name = "app", CompositeTypes = [new CompositeType { Name = "c", Fields = [new CompositeField("f", SqlType.Custom("jsonb"))] }] });

        diff.Schemas.ShouldBeEmpty();
    }

    private static ColumnDiff? DiffColumn(SqlEquivalence equivalence, Column current, Column desired) =>
        DiffSchemas(equivalence,
            new Schema { Name = "app", Tables = [new Table { Name = "t", Columns = [current] }] },
            new Schema { Name = "app", Tables = [new Table { Name = "t", Columns = [desired] }] })
        .Schemas.SingleOrDefault()?.Tables.SingleOrDefault()?.Columns.SingleOrDefault();

    private static DatabaseDiff DiffSchemas(SqlEquivalence equivalence, Schema current, Schema desired)
    {
        var sut = new DatabaseComparer(NullLogger<DatabaseComparer>.Instance, equivalence);
        var aligned = DatabaseAligner.Align(
            new Database { Schemas = [current] }, new Database { Schemas = [desired] }, ProjectDirectives.Empty);
        return sut.Compare(aligned.Require(), new Database { Schemas = [desired] });
    }

    /// <summary>A dialect rule folding a literal's trailing <c>::text</c> cast and a <c>pg_catalog</c> type qualifier.</summary>
    private sealed class CastFoldingEquivalence : SqlEquivalence
    {
        public override IEqualityComparer<SqlDefaultExpression> Defaults { get; } = new CastFolding();

        public override IEqualityComparer<SqlType> Types { get; } = new QualifierFolding();

        private sealed class CastFolding : IEqualityComparer<SqlDefaultExpression>
        {
            public bool Equals(SqlDefaultExpression? x, SqlDefaultExpression? y) => Fold(x) == Fold(y);

            public int GetHashCode(SqlDefaultExpression obj) => Fold(obj)!.GetHashCode();

            private static string? Fold(SqlDefaultExpression? value) =>
                value?.Value is { } v && v.EndsWith("::text", StringComparison.Ordinal) ? v[..^6] : value?.Value;
        }

        private sealed class QualifierFolding : IEqualityComparer<SqlType>
        {
            public bool Equals(SqlType? x, SqlType? y) => object.Equals(Fold(x), Fold(y));

            public int GetHashCode(SqlType obj) => Fold(obj)!.GetHashCode();

            private static SqlType? Fold(SqlType? type) =>
                type?.Schema == "pg_catalog" ? type with { Schema = null } : type;
        }
    }
}
