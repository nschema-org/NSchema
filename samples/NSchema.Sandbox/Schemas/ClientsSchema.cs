using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class ClientsSchema : AbstractSchemaProvider
{
    public ClientsSchema()
    {
        var clients = Schema("clients")
            .Comment("Schema for client and policy management.");

        var insurers = clients.Table("insurers")
            .Comment("Stores information about all the insurance companies we work for.");
        insurers.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        insurers.Column("name", SqlType.Custom("citext")).NotNull().Comment("Business name of the insurer.");
        insurers.PrimaryKey("pk_insurers", ["id"]);
        insurers.Index("uc_insurers_name", ["name"]).Unique();

        var policyTypes = clients.Table("policy_types")
            .Comment("Stores types of insurance policies that Abodio manages.");
        policyTypes.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        policyTypes.Column("name", SqlType.Custom("citext")).NotNull().Comment("Name of the policy type. Must be unique.");
        policyTypes.Column("description", SqlType.Custom("citext")).NotNull().Default("''").Comment("Description of the policy type.");
        policyTypes.Column("is_domestic", SqlType.Boolean).NotNull().Default("false").Comment("Indicates whether this policy type covers domestic properties.");
        policyTypes.Column("is_commercial", SqlType.Boolean).NotNull().Default("false").Comment("Indicates whether this policy type covers commercial properties.");
        policyTypes.PrimaryKey("pk_policy_types", ["id"]);
        policyTypes.Index("uc_policy_types_name", ["name"]).Unique();

        var policies = clients.Table("policies")
            .Comment("Stores policies that belong to insurers.");
        policies.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        policies.Column("name", SqlType.Custom("citext")).NotNull().Comment("Name of the policy.");
        policies.Column("insurer_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the insurer to whom this policy belongs.");
        policies.Column("policy_type_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the type of policy.");
        policies.PrimaryKey("pk_policies", ["id"]);
        policies.ForeignKey("fk_policies_insurer", ["insurer_id"], "clients", "insurers", ["id"]);
        policies.ForeignKey("fk_policies_policy_type", ["policy_type_id"], "clients", "policy_types", ["id"]);

        var lossAdjusters = clients.Table("loss_adjusters")
            .Comment("Stores information about all the loss adjusters we work with.");
        lossAdjusters.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        lossAdjusters.Column("name", SqlType.Custom("citext")).NotNull().Comment("Business name of the loss adjuster.");
        lossAdjusters.PrimaryKey("pk_loss_adjusters", ["id"]);
        lossAdjusters.Index("uc_loss_adjusters_name", ["name"]).Unique();

        var insurerReferences = clients.Table("insurer_references")
            .Comment("Describes reference numbers that must be given when a claim is logged for the given insurer.");
        insurerReferences.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        insurerReferences.Column("insurer_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the insurer.");
        insurerReferences.Column("name", SqlType.Custom("citext")).NotNull().Comment("Name of the reference.");
        insurerReferences.Column("input_mask", SqlType.Text).NotNull().Comment("A pattern that the reference number must match.");
        insurerReferences.Column("description", SqlType.Custom("citext")).NotNull().Comment("Description of the reference number.");
        insurerReferences.PrimaryKey("pk_insurer_references", ["id"]);
        insurerReferences.ForeignKey("fk_insurer_references_insurer", ["insurer_id"], "clients", "insurers", ["id"]);
    }
}
