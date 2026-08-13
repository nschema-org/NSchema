using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

/// <summary>
/// A recreate is a drop and a create, and no engine drops a type a column is still typed by. The plan has to
/// say so, rather than rendering happily and failing part-way through the apply.
/// </summary>
public sealed class RecreateDependentsTests
{
    private static readonly SqlIdentifier _app = new("app");

    /// <summary>A diff that retypes app.code, which is what makes a domain recreate.</summary>
    private static DatabaseDiff Retyped() => new()
    {
        Schemas =
        [
            SchemaDiff.Modified(_app) with { Domains = [ DomainDiff.Modified(_app, "code") with
                    {
                        DataType = new ValueChange<SqlType>(SqlType.VarChar(10), SqlType.VarChar(20)),
                    },
                ],
            },
        ],
    };

    /// <summary>The domain, and a table whose column is or is not typed by it.</summary>
    private static Database Current(bool inUse) => new()
    {
        Schemas =
        [
            new Schema
            {
                Name = _app,
                Domains = { new DomainType { Name = "code", DataType = SqlType.VarChar(10) } },
                Tables =
                [
                    new Table
                    {
                        Name = "parts",
                        Columns = { new Column { Name = "reference", Type = inUse ? new SqlType("code") { Schema = _app } : SqlType.Int } },
                    },
                ],
            },
        ],
    };

    [Fact]
    public void Check_ADomainRecreatedWithAColumnTypedByIt_IsBlocked()
    {
        // Act
        var diagnostics = RecreateDependents.Check(Retyped(), Current(inUse: true)).ToList();

        // Assert — the column is named, because that is what has to move before the change can be planned.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("recreate-blocked-by-dependents");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain("app.parts.reference");
        diagnostic.Message.ShouldContain("app.code");
    }

    [Fact]
    public void Check_ADomainRecreatedWithNothingTypedByIt_IsAllowed()
    {
        // Act
        var diagnostics = RecreateDependents.Check(Retyped(), Current(inUse: false));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_ADomainChangedButNotRecreated_IsAllowed()
    {
        // Arrange — a default or not-null change alters the domain in place, so nothing is dropped.
        var diff = new DatabaseDiff
        {
            Schemas = [SchemaDiff.Modified(_app) with { Domains = [DomainDiff.Modified(_app, "code")] }],
        };

        // Act
        var diagnostics = RecreateDependents.Check(diff, Current(inUse: true));

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
