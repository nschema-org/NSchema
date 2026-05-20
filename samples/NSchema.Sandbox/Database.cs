using NSchema.Domain.Schema;
using NSchema.Fluent;

namespace NSchema.Sandbox;

public static class Database
{
    public static DatabaseModel GetModel()
    {
        var desired = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("authors", table =>
                {
                    table.Column("id", SqlType.BigInt).NotNull().Identity();
                    table.Column("name", SqlType.VarChar(100)).NotNull();
                    table.Column("email", SqlType.VarChar(255)).NotNull();
                    table.Column("created_at", SqlType.DateTimeOffset).NotNull().Default("now()");
                    table.PrimaryKey("pk_authors", ["id"]);
                    table.Index("idx_authors_email", ["email"]).Unique();
                });

                schema.Table("posts", table =>
                {
                    table.Column("id", SqlType.BigInt).NotNull().Identity();
                    table.Column("author_id", SqlType.BigInt).NotNull();
                    table.Column("title", SqlType.VarChar(500)).NotNull();
                    table.Column("body", SqlType.Text).NotNull();
                    table.Column("published", SqlType.Boolean).NotNull().Default("false");
                    table.Column("created_at", SqlType.DateTimeOffset).NotNull().Default("now()");
                    table.PrimaryKey("pk_posts", ["id"]);
                    table.ForeignKey("fk_posts_author", ["author_id"], "public", "authors", ["id"])
                        .OnDelete(ReferentialAction.Cascade);
                    table.Index("idx_posts_author", ["author_id"]);
                });
            })
            .Build();
        return desired;
    }


}
