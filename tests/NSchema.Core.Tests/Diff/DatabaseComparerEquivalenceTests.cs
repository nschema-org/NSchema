using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
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

    [Fact]
    public void Compare_SequenceOptionEqualToTheEngineDefault_NeutralEquivalence_ReportsChange()
    {
        var diff = DiffSequence(new SqlEquivalence(),
            new Sequence { Name = "q" },
            new Sequence { Name = "q", Options = new SequenceOptions(StartWith: 1) });

        diff.ShouldNotBeNull().Options.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_SequenceOptionEqualToTheEngineDefault_DialectEquivalence_ReportsNoChange()
    {
        // A catalog reports a start whatever was declared, so the engine's own default and an explicit one are the
        // same schema; only the dialect knows which value that is.
        var diff = DiffSequence(new DefaultFoldingEquivalence(),
            new Sequence { Name = "q" },
            new Sequence { Name = "q", Options = new SequenceOptions(StartWith: 1) });

        diff.ShouldBeNull();
    }

    [Fact]
    public void Compare_SequenceOptionDifferingFromTheEngineDefault_StillReportsChange()
        => DiffSequence(new DefaultFoldingEquivalence(),
                new Sequence { Name = "q" },
                new Sequence { Name = "q", Options = new SequenceOptions(StartWith: 100) })
            .ShouldNotBeNull().Options.ShouldNotBeNull();

    [Fact]
    public void Compare_IdentityOptionEqualToTheEngineDefault_DialectEquivalence_ReportsNoChange()
    {
        var column = DiffColumn(new DefaultFoldingEquivalence(),
            new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) },
            new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = null });

        column.ShouldBeNull();
    }

    [Fact]
    public void Compare_IdentityOptionDifferingFromTheEngineDefault_StillReportsChange()
    {
        var column = DiffColumn(new DefaultFoldingEquivalence(),
            new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) },
            new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(StartWith: 50, null, null) });

        column.ShouldNotBeNull().Identity.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_ChangedSequence_CarriesTheFoldedOptions()
    {
        // The change is what the dialect writes from: restating a default it never asked to change can restart a
        // live counter, so the options that reach it are the folded ones, not the declared ones.
        var diff = DiffSequence(new DefaultFoldingEquivalence(),
            new Sequence { Name = "q" },
            new Sequence { Name = "q", Options = new SequenceOptions(StartWith: 1, Cache: 20) });

        var options = diff.ShouldNotBeNull().Options.ShouldNotBeNull().New.ShouldNotBeNull();
        options.StartWith.ShouldBeNull();
        options.Cache.ShouldBe(20);
    }

    private static SequenceDiff? DiffSequence(SqlEquivalence equivalence, Sequence current, Sequence desired) =>
        DiffSchemas(equivalence,
            new Schema { Name = "app", Sequences = [current] },
            new Schema { Name = "app", Sequences = [desired] })
        .Schemas.SingleOrDefault()?.Sequences.SingleOrDefault();

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

    /// <summary>A dialect rule whose sequences and identities start at 1 and increment by 1 unless told otherwise.</summary>
    private sealed class DefaultFoldingEquivalence : SqlEquivalence
    {
        public override SequenceOptions WithDefaults(SequenceOptions options) => options with
        {
            StartWith = options.StartWith == 1 ? null : options.StartWith,
            IncrementBy = options.IncrementBy == 1 ? null : options.IncrementBy,
            MinValue = options.MinValue == 1 ? null : options.MinValue,
        };

        public override IdentityOptions WithDefaults(IdentityOptions options, SqlType columnType) => options with
        {
            StartWith = options.StartWith == 1 ? null : options.StartWith,
            IncrementBy = options.IncrementBy == 1 ? null : options.IncrementBy,
            MinValue = options.MinValue == 1 ? null : options.MinValue,
        };
    }
}
