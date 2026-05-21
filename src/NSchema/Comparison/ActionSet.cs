using NSchema.Domain.Migration.Actions;

namespace NSchema.Comparison;

internal sealed class ActionSet
{
    private readonly List<SchemaAction> _preScripts = [];
    private readonly List<SchemaAction> _foreignKeyDrops = [];
    private readonly List<SchemaAction> _indexDrops = [];
    private readonly List<SchemaAction> _primaryKeyDrops = [];
    private readonly List<SchemaAction> _schemaRenames = [];
    private readonly List<SchemaAction> _schemaCreates = [];
    private readonly List<SchemaAction> _tableRenames = [];
    private readonly List<SchemaAction> _tableCreates = [];
    private readonly List<SchemaAction> _columnDrops = [];
    private readonly List<SchemaAction> _columnRenames = [];
    private readonly List<SchemaAction> _columnAdds = [];
    private readonly List<SchemaAction> _columnAlters = [];
    private readonly List<SchemaAction> _primaryKeyAdds = [];
    private readonly List<SchemaAction> _foreignKeyAdds = [];
    private readonly List<SchemaAction> _indexAdds = [];
    private readonly List<SchemaAction> _tableDrops = [];
    private readonly List<SchemaAction> _schemaDrops = [];
    private readonly List<SchemaAction> _postScripts = [];

    public void Add(SchemaAction action)
    {
        var bucket = action switch
        {
            RunPreDeploymentScript => _preScripts,
            RunPostDeploymentScript => _postScripts,
            DropForeignKey => _foreignKeyDrops,
            DropIndex => _indexDrops,
            DropPrimaryKey => _primaryKeyDrops,
            RenameSchema => _schemaRenames,
            CreateSchema => _schemaCreates,
            RenameTable => _tableRenames,
            CreateTable => _tableCreates,
            DropColumn => _columnDrops,
            RenameColumn => _columnRenames,
            AddColumn => _columnAdds,
            AlterColumnType => _columnAlters,
            AlterColumnNullability => _columnAlters,
            SetColumnDefault => _columnAlters,
            AddPrimaryKey => _primaryKeyAdds,
            AddForeignKey => _foreignKeyAdds,
            CreateIndex => _indexAdds,
            DropTable => _tableDrops,
            DropSchema => _schemaDrops,
            _ => throw new InvalidOperationException($"Unhandled action type: {action.GetType().Name}")
        };
        bucket.Add(action);
    }

    public List<SchemaAction> ToList() =>
    [
        .._preScripts,
        .._foreignKeyDrops,
        .._indexDrops,
        .._primaryKeyDrops,
        .._schemaRenames,
        .._schemaCreates,
        .._tableRenames,
        .._tableCreates,
        .._columnDrops,
        .._columnRenames,
        .._columnAdds,
        .._columnAlters,
        .._primaryKeyAdds,
        .._foreignKeyAdds,
        .._indexAdds,
        .._tableDrops,
        .._schemaDrops,
        .._postScripts,
    ];
}
