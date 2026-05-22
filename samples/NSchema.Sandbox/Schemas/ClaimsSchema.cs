using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class ClaimsSchema : AbstractSchemaProvider
{
    public ClaimsSchema()
    {
        var claims = Schema("claims")
            .Comment("Schema for claim logging.")
            .Grant(Roles.Api)
            .AddEventsTable(Roles.Api)
            .AddIntegrationEventsTable(Roles.Api)
            .AddProjectionsTable(Roles.Api);

        AddDamageSources(claims);
        AddPerils(claims);
        AddClaimTypes(claims);
        AddClaimStatuses(claims);
        AddClaimPriorities(claims);
        AddProjectionClaimSummaries(claims);
    }

    private static void AddDamageSources(SchemaBuilder schema)
    {
        var table = schema.Table("damage_sources")
            .Comment("Stores information about all damage sources that might cause a claim.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the damage source.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of the damage source.");
        table.PrimaryKey("pk_damage_sources", ["id"]);
        table.Index("uc_damage_sources_name", ["name"]).Unique();
    }

    private static void AddPerils(SchemaBuilder schema)
    {
        var table = schema.Table("perils")
            .Comment("Stores information about all perils that might need to be resolved as part of a claim.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the peril.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of the peril.");
        table.PrimaryKey("pk_perils", ["id"]);
        table.Index("uc_perils_name", ["name"]).Unique();
    }

    private static void AddClaimTypes(SchemaBuilder schema)
    {
        var table = schema.Table("claim_types")
            .Comment("Stores information about all the different claim types.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the claim type.");
        table.Column("description", SqlType.Citext).NotNull().Comment("A description of the claim type.");
        table.Column("covers_building", SqlType.Boolean).NotNull().Default("false").Comment("Whether the claim type covers the building.");
        table.Column("covers_contents", SqlType.Boolean).NotNull().Default("false").Comment("Whether the claim type covers the contents of a building.");
        table.PrimaryKey("pk_claim_types", ["id"]);
        table.Index("uc_claim_types_name", ["name"]).Unique();
    }

    private static void AddClaimStatuses(SchemaBuilder schema)
    {
        var table = schema.Table("claim_statuses")
            .Comment("Stores information about all the different claim statuses.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the claim status.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of the claim status.");
        table.PrimaryKey("pk_claim_statuses", ["id"]);
        table.Index("uc_claim_statuses_name", ["name"]).Unique();
    }

    private static void AddClaimPriorities(SchemaBuilder schema)
    {
        var table = schema.Table("claim_priorities")
            .Comment("Stores information about all the different claim priority levels.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("The name of the claim priority.");
        table.Column("description", SqlType.Citext).NotNull().Comment("A description of the claim priority.");
        table.Column("priority_order", SqlType.Int).NotNull().Default("0").Comment("Ordinal indicating urgency (lower is more important).");
        table.Column("color", SqlType.Citext).Comment("The color indicator used to denote claims of this priority level.");
        table.PrimaryKey("pk_claim_priorities", ["id"]);
        table.Index("uc_claim_priorities_name", ["name"]).Unique();
    }

    private static void AddProjectionClaimSummaries(SchemaBuilder schema)
    {
        var table = schema.Table("projection_claim_summaries")
            .Comment("Stores a projected view of the most up to date claim dashboard data.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("type_id", SqlType.TypeId).NotNull().Comment("The id for the claim type.");
        table.Column("type_name", SqlType.Text).NotNull().Comment("The name for the claim type.");
        table.Column("status_id", SqlType.TypeId).NotNull().Comment("The id for the claim status.");
        table.Column("status_name", SqlType.Text).NotNull().Comment("The name for the claim status.");
        table.PrimaryKey("pk_projection_claim_summaries", ["id"]);
    }
}
