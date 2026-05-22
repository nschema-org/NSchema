using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class ClientsSchema : AbstractSchemaProvider
{
    public ClientsSchema()
    {
        var clients = Schema("clients")
            .Comment("Schema for client and policy management.")
            .Grant(Roles.Api);

        AddInsurers(clients);
        AddPolicyTypes(clients);
        AddPolicies(clients);
        AddLossAdjusters(clients);
        AddInsurerReferences(clients);
    }

    private static void AddInsurers(SchemaBuilder schema)
    {
        var table = schema.Table("insurers")
            .Comment("Stores information about all the insurance companies we work for.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Business name of the insurer.");
        table.PrimaryKey("pk_insurers", ["id"]);
        table.Index("uc_insurers_name", ["name"]).Unique();
    }

    private static void AddPolicyTypes(SchemaBuilder schema)
    {
        var table = schema.Table("policy_types")
            .Comment("Stores types of insurance policies that Abodio manages.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the policy type. Must be unique.");
        table.Column("description", SqlType.Citext).NotNull().Default("''").Comment("Description of the policy type.");
        table.Column("is_domestic", SqlType.Boolean).NotNull().Default("false").Comment("Indicates whether this policy type covers domestic properties.");
        table.Column("is_commercial", SqlType.Boolean).NotNull().Default("false").Comment("Indicates whether this policy type covers commercial properties.");
        table.PrimaryKey("pk_policy_types", ["id"]);
        table.Index("uc_policy_types_name", ["name"]).Unique();
    }

    private static void AddPolicies(SchemaBuilder schema)
    {
        var table = schema.Table("policies")
            .Comment("Stores policies that belong to insurers.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the policy.");
        table.Column("insurer_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the insurer to whom this policy belongs.");
        table.Column("policy_type_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the type of policy.");
        table.PrimaryKey("pk_policies", ["id"]);
        table.ForeignKey("fk_policies_insurer", ["insurer_id"], "clients", "insurers", ["id"]);
        table.ForeignKey("fk_policies_policy_type", ["policy_type_id"], "clients", "policy_types", ["id"]);
    }

    private static void AddLossAdjusters(SchemaBuilder schema)
    {
        var table = schema.Table("loss_adjusters")
            .Comment("Stores information about all the loss adjusters we work with.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Business name of the loss adjuster.");
        table.PrimaryKey("pk_loss_adjusters", ["id"]);
        table.Index("uc_loss_adjusters_name", ["name"]).Unique();
    }

    private static void AddInsurerReferences(SchemaBuilder schema)
    {
        var table = schema.Table("insurer_references")
            .Comment("Describes reference numbers that must be given when a claim is logged for the given insurer.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("insurer_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the insurer.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the reference (e.g. \"Aviva Reference\").");
        table.Column("input_mask", SqlType.Text).NotNull().Comment("A pattern that the reference number must match.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of the reference number and what it is used for.");
        table.PrimaryKey("pk_insurer_references", ["id"]);
        table.ForeignKey("fk_insurer_references_insurer", ["insurer_id"], "clients", "insurers", ["id"]);
    }
}
