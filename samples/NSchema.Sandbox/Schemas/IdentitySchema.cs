using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

public class IdentitySchema : AbstractSchemaProvider
{
    public IdentitySchema()
    {
        var identity = Schema("identity")
            .Comment("Schema for identity and access management, including users, roles, and permissions.");

        var users = identity.Table("users")
            .Comment("Stores information about all users.");
        users.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        users.Column("name", SqlType.Custom("citext")).NotNull().Comment("Full name of the user.");
        users.Column("email", SqlType.Custom("citext")).NotNull().Comment("Email address of the user. Must be unique (case insensitive).");
        users.Column("avatar_uri", SqlType.Custom("citext")).Comment("URI to the user's avatar.");
        users.Column("identity_provider_id", SqlType.Text).Comment("Identifier from the external identity provider (AWS Cognito).");
        users.PrimaryKey("pk_users", ["id"]);
        users.Index("uc_users_email", ["email"]).Unique();
        users.Index("uc_users_identity_provider_id", ["identity_provider_id"]).Unique();

        var profiles = identity.Table("profiles")
            .Comment("Stores profile information for users.");
        profiles.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        profiles.Column("name", SqlType.Custom("citext")).NotNull().Comment("Name of the profile.");
        profiles.Column("user_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the user to whom this profile belongs.");
        profiles.PrimaryKey("pk_profiles", ["id"]);
        profiles.ForeignKey("fk_profiles_user", ["user_id"], "identity", "users", ["id"]);

        var roles = identity.Table("roles")
            .Comment("Authorization roles that can be assigned to user profiles.");
        roles.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        roles.Column("name", SqlType.Text).NotNull().Comment("Unique name of the role. Appears in access tokens.");
        roles.Column("friendly_name", SqlType.Custom("citext")).NotNull().Comment("Human-readable name of the role.");
        roles.Column("description", SqlType.Custom("citext")).NotNull().Comment("Description of the role and its purpose.");
        roles.Column("is_system_role", SqlType.Boolean).NotNull().Default("false").Comment("Indicates if the role is a system role.");
        roles.PrimaryKey("pk_roles", ["id"]);
        roles.Index("uc_roles_name", ["name"]).Unique();

        var permissions = identity.Table("permissions")
            .Comment("Access control permissions that can be assigned to roles.");
        permissions.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        permissions.Column("name", SqlType.Text).NotNull().Comment("Unique name of the permission. Appears in access tokens.");
        permissions.Column("friendly_name", SqlType.Custom("citext")).NotNull().Comment("Human-readable name of the permission.");
        permissions.Column("description", SqlType.Custom("citext")).NotNull().Comment("Description of what access the permission grants.");
        permissions.PrimaryKey("pk_permissions", ["id"]);
        permissions.Index("uc_permissions_name", ["name"]).Unique();

        var profileRoles = identity.Table("profile_roles")
            .Comment("Associative table linking user profiles to their assigned roles.");
        profileRoles.Column("profile_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the profile.");
        profileRoles.Column("role_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the role.");
        profileRoles.PrimaryKey("pk_profile_roles", ["profile_id", "role_id"]);
        profileRoles.ForeignKey("fk_profile_roles_profile", ["profile_id"], "identity", "profiles", ["id"]);
        profileRoles.ForeignKey("fk_profile_roles_role", ["role_id"], "identity", "roles", ["id"]);

        var rolePermissions = identity.Table("role_permissions")
            .Comment("Associative table linking roles to their assigned permissions.");
        rolePermissions.Column("role_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the role.");
        rolePermissions.Column("permission_id", SqlType.Custom("typeid")).NotNull().Comment("Foreign key referencing the permission.");
        rolePermissions.PrimaryKey("pk_role_permissions", ["role_id", "permission_id"]);
        rolePermissions.ForeignKey("fk_role_permissions_role", ["role_id"], "identity", "roles", ["id"]);
        rolePermissions.ForeignKey("fk_role_permissions_permission", ["permission_id"], "identity", "permissions", ["id"]);

        var audit = identity.Table("audit")
            .Comment("Audit log for tracking changes to permissions, roles, and profile assignments.");
        audit.Column("id", SqlType.Custom("typeid")).NotNull().Comment("Primary key.");
        audit.Column("event_type", SqlType.Text).NotNull().Comment("Type of event.");
        audit.Column("description", SqlType.Custom("citext")).NotNull().Comment("Description providing additional context.");
        audit.Column("user_id", SqlType.Custom("typeid")).Comment("Foreign key to the user.");
        audit.Column("user_name", SqlType.Custom("citext")).Comment("Name of the user.");
        audit.Column("profile_id", SqlType.Custom("typeid")).Comment("Foreign key to the profile.");
        audit.Column("profile_name", SqlType.Custom("citext")).Comment("Name of the profile.");
        audit.Column("role_id", SqlType.Custom("typeid")).Comment("Foreign key to the role.");
        audit.Column("role_name", SqlType.Custom("citext")).Comment("Name of the role.");
        audit.Column("permission_id", SqlType.Custom("typeid")).Comment("Foreign key to the permission.");
        audit.Column("permission_name", SqlType.Custom("citext")).Comment("Name of the permission.");
        audit.Column("changed_by_user_id", SqlType.Custom("typeid")).Comment("Foreign key to the user who made the change.");
        audit.Column("changed_by_user_name", SqlType.Custom("citext")).Comment("Name of the user who made the change.");
        audit.Column("created_at", SqlType.DateTimeOffset).NotNull().Comment("Timestamp when the change occurred.");
        audit.PrimaryKey("pk_audit", ["id"]);

        // NOTE: Partial indexes (WHERE clause) are not currently supported by NSchema.
        // The following indexes were defined with WHERE conditions in the original SQL:
        //   ix_audit_user_id       WHERE user_id IS NOT NULL
        //   ix_audit_profile_id    WHERE profile_id IS NOT NULL
        //   ix_audit_role_id       WHERE role_id IS NOT NULL
        //   ix_audit_permission_id WHERE permission_id IS NOT NULL
        audit.Index("ix_audit_event_type", ["event_type"]);
        audit.Index("ix_audit_user_id", ["user_id"]);
        audit.Index("ix_audit_profile_id", ["profile_id"]);
        audit.Index("ix_audit_role_id", ["role_id"]);
        audit.Index("ix_audit_permission_id", ["permission_id"]);
        audit.Index("ix_audit_changed_by", ["changed_by_user_id"]);
        audit.Index("ix_audit_created_at", ["created_at"]);

        var userActivity = identity.Table("user_activity")
            .Comment("Tracks the last time each user was seen making an API request.");
        userActivity.Column("user_id", SqlType.Custom("typeid")).NotNull().Comment("User ID (references identity.users).");
        userActivity.Column("last_seen_at", SqlType.DateTimeOffset).NotNull().Comment("Timestamp of the user's most recent API request.");
        userActivity.PrimaryKey("pk_user_activity", ["user_id"]);
    }
}
