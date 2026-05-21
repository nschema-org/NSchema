using NSchema.Domain.Schema;
using NSchema.Domain.Schema.Fluent;

namespace NSchema.Sandbox;

public class BooksSchema : AbstractSchemaProvider
{
    public BooksSchema()
    {
        var books = Schema("books");

        var authors = books.Table("authors");
        authors.Column("id", SqlType.BigInt).NotNull().Identity();
        authors.Column("name", SqlType.VarChar(100)).NotNull();
        authors.Column("email", SqlType.VarChar(255)).NotNull();
        authors.Column("created_at", SqlType.DateTimeOffset).NotNull().Default("now()");
        authors.PrimaryKey("pk_authors", ["id"]);
        authors.Index("idx_authors_email", ["email"]).Unique();

        var posts = books.Table("posts");
        posts.Column("id", SqlType.BigInt).NotNull().Identity();
        posts.Column("author_id", SqlType.BigInt).NotNull();
        posts.Column("title", SqlType.VarChar(500)).NotNull();
        posts.Column("body", SqlType.Text).NotNull();
        posts.Column("published", SqlType.Boolean).NotNull().Default("false");
        posts.Column("created_at", SqlType.DateTimeOffset).NotNull().Default("now()");
        posts.PrimaryKey("pk_posts", ["id"]);
        posts.ForeignKey("fk_posts_author", ["author_id"], "public", "authors", ["id"]).OnDelete(ReferentialAction.Cascade);
        posts.Index("idx_posts_author", ["author_id"]);
    }
}
