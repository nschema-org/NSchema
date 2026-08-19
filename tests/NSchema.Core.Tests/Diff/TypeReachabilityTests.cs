using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Types;

namespace NSchema.Tests.Diff;

public sealed class TypeReachabilityTests
{
    private static readonly SqlEquivalence _equivalence = new();

    private static readonly MemberAddress _body = new("app", "docs", "body");

    private static IEnumerable<Diagnostic> Check(Database desired, Database current, DatabaseDiff? diff = null, SqlEquivalence? equivalence = null) =>
        TypeReachability.Check(diff ?? new DatabaseDiff(), desired, current, equivalence ?? _equivalence);

    /// <summary>
    /// app.docs.body declared against <paramref name="columnType"/>, and nothing else.
    /// </summary>
    private static Database Desired(SqlType columnType) => new()
    {
        Schemas = [
        new Schema { Name = "app",
            Tables = [new Table { Name = "docs", Columns = [new Column { Name = "body", Type = columnType }] }] },
    ],
    };

    private static Database Current(params Schema[] schemas) => new() { Schemas = [.. schemas] };

    /// <summary>
    /// A captured engine vocabulary: the snapshot demonstrates it by holding native types.
    /// </summary>
    private static Schema Catalog(params string[] typeNames) => new()
    {
        Name = "pg_catalog",
        IsImplicit = true,
        NativeTypes = [.. typeNames.Select(name => new NativeType { Name = name })],
    };

    /// <summary>
    /// The ext schema holding citext, as the citext extension provides it.
    /// </summary>
    private static Schema CitextSchema() => new()
    {
        Name = "ext",
        IsImplicit = true,
        NativeTypes = [new NativeType { Name = "citext", ProvidedBy = new ExtensionReference("citext") }],
    };

    private sealed class Unvalidating : SqlEquivalence
    {
        public override bool ValidatesTypeNames => false;
    }

    [Fact]
    public void Check_QualifiedReferenceToACapturedNativeType_Resolves()
    {
        // Act — the Pagila case: a built-in reported under pg_catalog demands no declaration.
        var diagnostics = Check(Desired(SqlType.Custom("pg_catalog", "tsvector")), Current(Catalog("tsvector", "_text")));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_BareReferenceToACapturedNativeTypeInAnotherSchema_Resolves()
    {
        // Act — the engine's search path resolves what the model does not qualify.
        var diagnostics = Check(Desired(SqlType.Custom("citext")), Current(CitextSchema()));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_BareReferenceToNothing_Errors()
    {
        // Act — the other direction of the old spelling rule: a bare miss is caught, not waved through.
        var diagnostics = Check(Desired(SqlType.Custom("my_missing_type")), Current(Catalog("tsvector")));

        // Assert
        diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.UnresolvedTypes([_body], ["my_missing_type"]));
    }

    [Fact]
    public void Check_QualifiedReferenceToNothing_Errors()
    {
        // Act
        var diagnostics = Check(Desired(SqlType.Custom("app", "status")), Current(Catalog("tsvector")));

        // Assert
        diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.UnresolvedTypes([_body], ["app.status"]));
    }

    [Fact]
    public void Check_UnresolvedWhileThePlanInstallsExtensions_Hedges()
    {
        // Arrange — what a new extension provides cannot be known until it exists.
        var diff = new DatabaseDiff(null, [ExtensionDiff.Added(new Extension { Name = "citext" })]);

        // Act
        var diagnostics = Check(Desired(SqlType.Custom("citext")), Current(Catalog("tsvector")), diff);

        // Assert — softened to a warning naming the install, instead of guessing silently either way.
        diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.TypeMayComeFromExtension([_body], ["citext"], [new SqlIdentifier("citext")]));
    }

    [Fact]
    public void Check_ReferenceToATypeTheRemovedExtensionProvides_Errors()
    {
        // Arrange — dropping citext takes ext.citext with it; the reference outlives its type.
        var diff = new DatabaseDiff(null, [ExtensionDiff.Removed("citext")]);

        // Act
        var diagnostics = Check(Desired(SqlType.Custom("citext")), Current(CitextSchema()), diff);

        // Assert — the miss is the plan's own removal: positive knowledge, asserted as such.
        diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.RemovedTypeStillReferenced([_body], ["citext"]));
    }

    [Fact]
    public void Check_ReferenceToADeclaredType_Resolves()
    {
        // Arrange — the desired side declares the enum it references; nothing exists yet.
        var desired = new Database
        {
            Schemas = [
            new Schema { Name = "app",
                Enums = [new EnumType { Name = "status", Values = ["new", "done"] }],
                Tables = [new Table { Name = "docs", Columns = [new Column { Name = "body", Type = SqlType.Custom("app", "status") }] }] },
        ],
        };

        // Act
        var diagnostics = Check(desired, Current());

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_ReferenceToATypeThePlanRemoves_Errors()
    {
        // Arrange — the project dropped the enum but a column still names it: current has it, the plan
        // removes it, so after apply the reference dangles. Knowable without any captured vocabulary.
        var diff = new DatabaseDiff([SchemaDiff.Containing("app") with { Enums = [EnumDiff.Removed("app", "status")] }]);
        var current = Current(new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["new"] }] });

        // Act
        var diagnostics = Check(Desired(SqlType.Custom("app", "status")), current, diff);

        // Assert
        diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.RemovedTypeStillReferenced([_body], ["app.status"]));
    }

    [Fact]
    public void Check_NoCapturedVocabulary_QualifiedMiss_Warns()
    {
        // Act — a snapshot with no native types was never captured; absence proves nothing, so the miss
        // is hedged, not asserted.
        var diagnostics = Check(Desired(SqlType.Custom("app", "status")), Current());

        // Assert
        diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.UnverifiedTypes([_body], ["app.status"]));
    }

    [Fact]
    public void Check_NoCapturedVocabulary_BareMiss_IsSilent()
    {
        // Act — a bare name resolves against the engine's own vocabulary, which nothing has captured;
        // warning on every bare provider type would drown a plan in noise.
        var diagnostics = Check(Desired(SqlType.Custom("citext")), Current());

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_EngineThatDoesNotValidateTypeNames_IsSilent()
    {
        // Act — SQLite accepts any type name, so every reference resolves by definition.
        var diagnostics = Check(Desired(SqlType.Custom("no_such_thing", "anywhere")), Current(), equivalence: new Unvalidating());

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_FacetedReference_ResolvesByNameAlone()
    {
        // Act — existence is a question about the name; the facets are per-use.
        var diagnostics = Check(Desired(SqlType.VarChar(10)), Current(Catalog("varchar")));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_CompositeFieldsTypedByACapturedExtensionType_Resolve()
    {
        // Arrange — a composite of (citext, citext): both fields reference the one captured type.
        var desired = new Database
        {
            Schemas = [
            new Schema { Name = "app",
                CompositeTypes = [new CompositeType { Name = "pair", Fields = [new("first", SqlType.Custom("citext")), new("second", SqlType.Custom("citext"))] }] },
        ],
        };

        // Act
        var diagnostics = Check(desired, Current(CitextSchema()));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Check_DomainOverACapturedExtensionType_Resolves()
    {
        // Arrange — a domain over citext references the captured native type as its base.
        var desired = new Database
        {
            Schemas = [
            new Schema { Name = "app",
                Domains = [new DomainType { Name = "email", DataType = SqlType.Custom("citext") }] },
        ],
        };

        // Act
        var diagnostics = Check(desired, Current(CitextSchema()));

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
