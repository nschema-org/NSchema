using System.Text.Json.Serialization;

namespace NSchema.Domain.Execution;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CreateSchema), "create_schema")]
[JsonDerivedType(typeof(DropSchema), "drop_schema")]
[JsonDerivedType(typeof(RenameSchema), "rename_schema")]
[JsonDerivedType(typeof(CreateTable), "create_table")]
[JsonDerivedType(typeof(DropTable), "drop_table")]
[JsonDerivedType(typeof(RenameTable), "rename_table")]
[JsonDerivedType(typeof(AddColumn), "add_column")]
[JsonDerivedType(typeof(DropColumn), "drop_column")]
[JsonDerivedType(typeof(RenameColumn), "rename_column")]
[JsonDerivedType(typeof(AlterColumnType), "alter_column_type")]
[JsonDerivedType(typeof(AlterColumnNullability), "alter_column_nullability")]
[JsonDerivedType(typeof(SetColumnDefault), "set_column_default")]
[JsonDerivedType(typeof(AddPrimaryKey), "add_primary_key")]
[JsonDerivedType(typeof(DropPrimaryKey), "drop_primary_key")]
[JsonDerivedType(typeof(AddForeignKey), "add_foreign_key")]
[JsonDerivedType(typeof(DropForeignKey), "drop_foreign_key")]
[JsonDerivedType(typeof(CreateIndex), "create_index")]
[JsonDerivedType(typeof(DropIndex), "drop_index")]
[JsonDerivedType(typeof(RunPreDeploymentScript), "run_pre_deployment_script")]
[JsonDerivedType(typeof(RunPostDeploymentScript), "run_post_deployment_script")]
public abstract record SchemaInstruction
{
    public abstract bool IsDestructive { get; }
}
