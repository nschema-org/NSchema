using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class IdentitySchema : AbstractSchemaProvider
{
    public IdentitySchema()
    {
        var identity = Schema("identity")
            .Comment("Schema for identity and access management, including users, roles, and permissions.")
            .Grant(Roles.Api);

        AddUsers(identity);
        AddProfiles(identity);
        AddRoles(identity);
        AddPermissions(identity);
        AddProfileRoles(identity);
        AddRolePermissions(identity);
        AddAudit(identity);
        AddUserActivity(identity);
    }

    private static void AddUsers(SchemaBuilder schema)
    {
        var table = schema.Table("users")
            .Comment("Stores information about all users.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Full name of the user.");
        table.Column("email", SqlType.Citext).NotNull().Comment("Email address of the user. Must be unique (case insensitive).");
        table.Column("avatar_uri", SqlType.Citext).Comment("URI to the user's avatar.");
        table.Column("identity_provider_id", SqlType.Text).Comment("Identifier from the external identity provider (AWS Cognito).");
        table.PrimaryKey("pk_users", ["id"]);
        table.Index("uc_users_email", ["email"]).Unique();
        table.Index("uc_users_identity_provider_id", ["identity_provider_id"]).Unique();
    }

    private static void AddProfiles(SchemaBuilder schema)
    {
        var table = schema.Table("profiles")
            .Comment("Stores profile information for users.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Citext).NotNull().Comment("Name of the profile.");
        table.Column("user_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the user to whom this profile belongs.");
        table.PrimaryKey("pk_profiles", ["id"]);
        table.ForeignKey("fk_profiles_user", ["user_id"], "identity", "users", ["id"]);
    }

    private static void AddRoles(SchemaBuilder schema)
    {
        var table = schema.Table("roles")
            .Comment("Authorization roles that can be assigned to user profiles.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Text).NotNull().Comment("Unique name of the role. Appears in access tokens.");
        table.Column("friendly_name", SqlType.Citext).NotNull().Comment("Human-readable name of the role.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of the role and its purpose.");
        table.Column("is_system_role", SqlType.Boolean).NotNull().Default("false")
            .Comment("Indicates if the role is a system role. System roles have special privileges and cannot be deleted.");
        table.PrimaryKey("pk_roles", ["id"]);
        table.Index("uc_roles_name", ["name"]).Unique();
    }

    private static void AddPermissions(SchemaBuilder schema)
    {
        var table = schema.Table("permissions")
            .Comment("Access control permissions that can be assigned to roles.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("name", SqlType.Text).NotNull().Comment("Unique name of the permission. Appears in access tokens.");
        table.Column("friendly_name", SqlType.Citext).NotNull().Comment("Human-readable name of the permission.");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description of what access the permission grants.");
        table.PrimaryKey("pk_permissions", ["id"]);
        table.Index("uc_permissions_name", ["name"]).Unique();
    }

    private static void AddProfileRoles(SchemaBuilder schema)
    {
        var table = schema.Table("profile_roles")
            .Comment("Associative table linking user profiles to their assigned roles.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("profile_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the profile.");
        table.Column("role_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the role.");
        table.PrimaryKey("pk_profile_roles", ["profile_id", "role_id"]);
        table.ForeignKey("fk_profile_roles_profile", ["profile_id"], "identity", "profiles", ["id"]);
        table.ForeignKey("fk_profile_roles_role", ["role_id"], "identity", "roles", ["id"]);
    }

    private static void AddRolePermissions(SchemaBuilder schema)
    {
        var table = schema.Table("role_permissions")
            .Comment("Associative table linking roles to their assigned permissions.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("role_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the role.");
        table.Column("permission_id", SqlType.TypeId).NotNull().Comment("Foreign key referencing the permission.");
        table.PrimaryKey("pk_role_permissions", ["role_id", "permission_id"]);
        table.ForeignKey("fk_role_permissions_role", ["role_id"], "identity", "roles", ["id"]);
        table.ForeignKey("fk_role_permissions_permission", ["permission_id"], "identity", "permissions", ["id"]);
    }

    private static void AddAudit(SchemaBuilder schema)
    {
        var table = schema.Table("audit")
            .Comment("Audit log for tracking changes to permissions, roles, and profile assignments.")
            .Grant(Roles.Api, TablePrivilege.AppendOnly);
        table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
        table.Column("event_type", SqlType.Text).NotNull().Comment("Type of event (e.g. role_permission_added, profile_role_removed).");
        table.Column("description", SqlType.Citext).NotNull().Comment("Description providing additional context about the change.");
        table.Column("user_id", SqlType.TypeId).Comment("Foreign key to the affected user.");
        table.Column("user_name", SqlType.Citext).Comment("Name of the affected user.");
        table.Column("profile_id", SqlType.TypeId).Comment("Foreign key to the affected profile.");
        table.Column("profile_name", SqlType.Citext).Comment("Name of the affected profile.");
        table.Column("role_id", SqlType.TypeId).Comment("Foreign key to the affected role.");
        table.Column("role_name", SqlType.Citext).Comment("Name of the affected role.");
        table.Column("permission_id", SqlType.TypeId).Comment("Foreign key to the affected permission.");
        table.Column("permission_name", SqlType.Citext).Comment("Name of the affected permission.");
        table.Column("changed_by_user_id", SqlType.TypeId).Comment("Foreign key to the user who made the change.");
        table.Column("changed_by_user_name", SqlType.Citext).Comment("Name of the user who made the change.");
        table.Column("created_at", SqlType.DateTimeOffset).NotNull().Comment("Timestamp when the change occurred.");
        table.PrimaryKey("pk_audit", ["id"]);
        table.Index("ix_audit_event_type", ["event_type"]);
        table.Index("ix_audit_user_id", ["user_id"]).Where("user_id IS NOT NULL");
        table.Index("ix_audit_profile_id", ["profile_id"]).Where("profile_id IS NOT NULL");
        table.Index("ix_audit_role_id", ["role_id"]).Where("role_id IS NOT NULL");
        table.Index("ix_audit_permission_id", ["permission_id"]).Where("permission_id IS NOT NULL");
        table.Index("ix_audit_changed_by", ["changed_by_user_id"]);
        table.Index("ix_audit_created_at", ["created_at"]);
    }

    private static void AddUserActivity(SchemaBuilder schema)
    {
        var table = schema.Table("user_activity")
            .Comment("Tracks the last time each user was seen making an API request.")
            .Grant(Roles.Api, TablePrivilege.All);
        table.Column("user_id", SqlType.TypeId).NotNull().Comment("User ID (references identity.users).");
        table.Column("last_seen_at", SqlType.DateTimeOffset).NotNull().Comment("Timestamp of the user's most recent API request.");
        table.PrimaryKey("pk_user_activity", ["user_id"]);
    }
}
