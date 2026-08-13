using NSchema.Diff.Domain;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
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

    /// <summary>A diff that retypes one field of app.address in place.</summary>
    private static DatabaseDiff FieldRetyped() => new()
    {
        Schemas =
        [
            SchemaDiff.Modified(_app) with
            {
                CompositeTypes =
                [
                    CompositeTypeDiff.Modified(_app, "address") with
                    {
                        Fields = [CompositeFieldDiff.TypeChanged("city", new ValueChange<SqlType>(SqlType.VarChar(10), SqlType.VarChar(20)))],
                    },
                ],
            },
        ],
    };

    /// <summary>The composite type, and a table whose column is or is not typed by it.</summary>
    private static Database CurrentComposite(bool inUse) => new()
    {
        Schemas =
        [
            new Schema
            {
                Name = _app,
                CompositeTypes = { new CompositeType { Name = "address", Fields = [new CompositeField("city", SqlType.VarChar(10))] } },
                Tables =
                [
                    new Table
                    {
                        Name = "people",
                        Columns = { new Column { Name = "home", Type = inUse ? new SqlType("address") { Schema = _app } : SqlType.Int } },
                    },
                ],
            },
        ],
    };

    [Fact]
    public void Check_ACompositeFieldRetypedWithAColumnHoldingTheType_IsBlocked()
    {
        // Act
        var diagnostics = RecreateDependents.Check(FieldRetyped(), CurrentComposite(inUse: true)).ToList();

        // Assert — Postgres refuses ALTER TYPE … ALTER ATTRIBUTE outright while a column holds the type, and no
        // CASCADE gets round it, so the plan has to say so rather than fail part-way through the apply.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("retype-blocked-by-dependents");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain("app.people.home");
        diagnostic.Message.ShouldContain("app.address");
    }

    [Fact]
    public void Check_ACompositeFieldRetypedWithNothingHoldingTheType_IsAllowed()
        => RecreateDependents.Check(FieldRetyped(), CurrentComposite(inUse: false)).ShouldBeEmpty();

    [Fact]
    public void Check_ACompositeFieldAddedOrDropped_IsAllowed()
    {
        // Arrange — adding and dropping an attribute both work while the type is in use; only a retype does not.
        var diff = new DatabaseDiff
        {
            Schemas =
            [
                SchemaDiff.Modified(_app) with
                {
                    CompositeTypes =
                    [
                        CompositeTypeDiff.Modified(_app, "address") with
                        {
                            Fields =
                            [
                                CompositeFieldDiff.Added(new CompositeField("postcode", SqlType.VarChar(10))),
                                CompositeFieldDiff.Removed("city"),
                            ],
                        },
                    ],
                },
            ],
        };

        // Act & Assert
        RecreateDependents.Check(diff, CurrentComposite(inUse: true)).ShouldBeEmpty();
    }
}
