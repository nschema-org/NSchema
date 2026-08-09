using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

/// <summary>
/// <c>NOT FOR REPLICATION</c> on an identity and on a trigger.
/// </summary>
/// <remarks>
/// Losing it changes nothing about ordinary writes and everything about what happens when a replication agent
/// writes, which is the case nobody is watching.
/// </remarks>
public sealed class NotForReplicationTests
{
    private static Database DatabaseWith(bool notForReplication) => new()
    {
        Schemas = [new Schema
        {
            Name = "app",
            Tables =
            [
                new Table
                {
                    Name = "orders",
                    Columns =
                    [
                        new Column
                        {
                            Name = "id",
                            Type = SqlType.Int,
                            IsIdentity = true,
                            IdentityOptions = new IdentityOptions(1, null, 1, notForReplication),
                        },
                    ],
                    Triggers =
                    [
                        new Trigger
                        {
                            Name = "t_orders",
                            Timing = TriggerTiming.After,
                            Events = TriggerEvent.Insert,
                            Body = "BEGIN SELECT 1 END",
                            IsNotForReplication = notForReplication,
                        },
                    ],
                },
            ],
        }],
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Identity_SurvivesTheRoundTrip(bool notForReplication)
    {
        // Arrange
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(DatabaseWith(notForReplication), declareSchemas: false));

        // Act
        var reparsed = new TestNsqlParser(ddl).Parse().Database;

        // Assert
        reparsed.Schemas.Single().Tables.Single().Columns.Single()
            .IdentityOptions!.NotForReplication.ShouldBe(notForReplication);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Trigger_SurvivesTheRoundTrip(bool notForReplication)
    {
        // Arrange
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(DatabaseWith(notForReplication), declareSchemas: false));

        // Act
        var reparsed = new TestNsqlParser(ddl).Parse().Database;

        // Assert
        reparsed.Schemas.Single().Tables.Single().Triggers.Single()
            .IsNotForReplication.ShouldBe(notForReplication);
    }
}
