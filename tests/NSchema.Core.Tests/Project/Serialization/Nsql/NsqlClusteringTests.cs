using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Clustering says which index a relation's rows are physically ordered by. Written as T-SQL writes it, since
/// SQL Server is the engine that has it.
/// </summary>
public sealed class NsqlClusteringTests
{
    private static Schema ParseSchema(string sql) =>
        new TestNsqlParser("CREATE SCHEMA app; " + sql).Parse().Database.Schemas.ShouldHaveSingleItem();

    private static Table ParseTable(string sql) => ParseSchema(sql).Tables.ShouldHaveSingleItem();

    [Theory]
    [InlineData("CLUSTERED", true)]
    [InlineData("NONCLUSTERED", false)]
    public void Parse_PrimaryKey_CarriesItsClustering(string keyword, bool clustered)
        => ParseTable($"CREATE TABLE app.t (id int, CONSTRAINT t_pk PRIMARY KEY {keyword} (id));")
            .PrimaryKey!.Clustered.ShouldBe(clustered);

    [Theory]
    [InlineData("CLUSTERED", true)]
    [InlineData("NONCLUSTERED", false)]
    public void Parse_UniqueConstraint_CarriesItsClustering(string keyword, bool clustered)
        => ParseTable($"CREATE TABLE app.t (id int, CONSTRAINT t_uq UNIQUE {keyword} (id));")
            .UniqueConstraints.ShouldHaveSingleItem().Clustered.ShouldBe(clustered);

    [Theory]
    [InlineData("CREATE CLUSTERED INDEX t_ix ON app.t (id);", true)]
    [InlineData("CREATE NONCLUSTERED INDEX t_ix ON app.t (id);", false)]
    [InlineData("CREATE UNIQUE CLUSTERED INDEX t_ix ON app.t (id);", true)]
    public void Parse_StandaloneIndex_CarriesItsClustering(string statement, bool clustered)
        => ParseSchema($"CREATE TABLE app.t (id int); {statement}")
            .Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem().Clustered.ShouldBe(clustered);

    [Fact]
    public void Parse_InlineIndexMember_CarriesItsClustering()
        => ParseTable("CREATE TABLE app.t (id int, CLUSTERED INDEX t_ix (id));")
            .Indexes.ShouldHaveSingleItem().Clustered.ShouldBe(true);

    /// <summary>
    /// Saying nothing is its own state: the engines disagree on the default (a SQL Server primary key clusters,
    /// an index does not), so an undeclared clustering must not read back as <c>NONCLUSTERED</c>.
    /// </summary>
    [Fact]
    public void Parse_WithoutTheKeyword_LeavesClusteringUnspecified()
    {
        var table = ParseTable("CREATE TABLE app.t (id int, CONSTRAINT t_pk PRIMARY KEY (id), INDEX t_ix (id));");

        table.PrimaryKey!.Clustered.ShouldBeNull();
        table.Indexes.ShouldHaveSingleItem().Clustered.ShouldBeNull();
    }

    [Fact]
    public void Write_Clustering_RoundTripsThroughParse()
    {
        var written = NsqlWriter.Write(Database());

        written.ShouldContain("PRIMARY KEY NONCLUSTERED (id)");
        written.ShouldContain("UNIQUE CLUSTERED (code)");

        var table = new TestNsqlParser(written).Parse().Database.Schemas.ShouldHaveSingleItem()
            .Tables.ShouldHaveSingleItem();
        table.PrimaryKey!.Clustered.ShouldBe(false);
        table.UniqueConstraints.ShouldHaveSingleItem().Clustered.ShouldBe(true);
        table.Indexes.ShouldHaveSingleItem().Clustered.ShouldBe(false);
    }

    [Fact]
    public void Format_WrittenClustering_IsAlreadyCanonical()
    {
        var written = NsqlWriter.Write(Database());
        var result = NsqlWriter.Format(written);

        result.Value.ShouldBe(written);
        result.Warnings.ShouldBeEmpty();
    }

    // A table ordered by something other than its primary key — the shape that made this worth modelling.
    private static Database Database() => new()
    {
        Schemas = [new Schema
        {
            Name = "app",
            Tables =
            [
                new Table
                {
                    Name = "t",
                    Columns =
                    [
                        new Column { Name = "id", Type = SqlType.Int },
                        new Column { Name = "code", Type = SqlType.Int },
                    ],
                    PrimaryKey = new PrimaryKey { Name = "t_pk", ColumnNames = ["id"], Clustered = false },
                    UniqueConstraints = { new UniqueConstraint { Name = "t_uq", ColumnNames = ["code"], Clustered = true } },
                    Indexes = { new TableIndex { Name = "t_ix", Columns = [new IndexColumn("code")], Clustered = false } },
                },
            ],
        }],
    };
}
