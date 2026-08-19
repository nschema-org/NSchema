using NSchema.Diff.Domain;
using NSchema.Diff.Domain.XmlSchemaCollections;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.XmlSchemaCollections;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // XML schema collections
    // -------------------------------------------------------------------------

    /// <summary>Diffs two <c>app</c> schemas holding the given collections, returning the single diff (null when unchanged).</summary>
    private XmlSchemaCollectionDiff? DiffXmlSchemaCollections(
        IReadOnlyList<XmlSchemaCollection> current, IReadOnlyList<XmlSchemaCollection> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", XmlSchemaCollections = [.. current] }), Db(new Schema { Name = "app", XmlSchemaCollections = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.XmlSchemaCollections.SingleOrDefault();

    private static XmlSchemaCollection Survey(string body, string? comment = null) =>
        new() { Name = "survey", Body = body, Comment = comment };

    private const string SurveyXsd = """<xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema"><xsd:element name="survey"/></xsd:schema>""";
    private const string RevisedXsd = """<xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema"><xsd:element name="revised"/></xsd:schema>""";

    [Fact]
    public void Compare_NewXmlSchemaCollection_IsAddCarryingDefinition()
    {
        var diff = DiffXmlSchemaCollections([], [Survey(SurveyXsd)]);

        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition!.Body.Value.ShouldBe(SurveyXsd);
    }

    [Fact]
    public void Compare_RemovedXmlSchemaCollection_IsRemove()
        => DiffXmlSchemaCollections([Survey(SurveyXsd)], [])!.Change.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedXmlSchemaCollection_ProducesNoDiff()
        => DiffXmlSchemaCollections([Survey(SurveyXsd)], [Survey(SurveyXsd)]).ShouldBeNull();

    [Fact]
    public void Compare_ChangedBody_IsRecreateCarryingDefinition()
    {
        var diff = DiffXmlSchemaCollections([Survey(SurveyXsd)], [Survey(RevisedXsd)]);

        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition!.Body.Value.ShouldBe(RevisedXsd);
    }

    [Fact]
    public void Compare_XmlSchemaCollection_CommentOnlyChange_IsModifyWithoutRecreate()
    {
        var diff = DiffXmlSchemaCollections([Survey(SurveyXsd, "old")], [Survey(SurveyXsd, "new")]);

        diff!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
        diff.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_RenamedXmlSchemaCollection_SetsRenamedFrom()
    {
        var diff = DiffXmlSchemaCollections(
            [new XmlSchemaCollection { Name = "legacy_survey", Body = SurveyXsd }],
            [Survey(SurveyXsd)],
            new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(
                new ObjectAddress("app", "legacy_survey", SchemaObjectKind.XmlSchemaCollection), "survey")]));

        diff!.RenamedFrom.ShouldBe("legacy_survey");
    }

    /// <summary>
    /// A schema whose only change is to its collections must carry them: the emptiness guard counts them, so
    /// dropping them from the projection leaves a diff that is non-empty, renders nothing, and plans nothing —
    /// a migration that reports changes it cannot name and can never converge.
    /// </summary>
    [Fact]
    public void Compare_SchemaChangedOnlyByItsXmlSchemaCollections_ProjectsThemOntoTheSchema()
    {
        var diff = Compare(
            Db(new Schema { Name = "app", XmlSchemaCollections = [Survey(SurveyXsd)] }),
            Db(new Schema { Name = "app", XmlSchemaCollections = [Survey(RevisedXsd)] }));

        var schema = diff.Schemas.ShouldHaveSingleItem();
        schema.XmlSchemaCollections.ShouldHaveSingleItem().Name.ShouldBe("survey");
    }
}
