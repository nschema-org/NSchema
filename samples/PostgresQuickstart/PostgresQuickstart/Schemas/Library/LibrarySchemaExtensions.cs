using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace PostgresQuickstart.Schemas.Library;

internal static class LibrarySchemaExtensions
{
    extension(SchemaBuilder schema)
    {
        public SchemaBuilder Authors() => schema.Table("authors", t => t
            .Comment("People who wrote books held by the library.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("authors_pkey").Comment("Primary key."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Full name of the author."))
            .Column("country", SqlType.Citext, c => c.Comment("Country of origin."))
            .Column("born_on", SqlType.Date, c => c.Comment("Date of birth, if known."))
            .Index("ix_authors_name", ["name"], _ => { })
        );

        public SchemaBuilder Books() => schema.Table("books", t => t
            .Comment("Books held in the library catalogue.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("books_pkey").Comment("Primary key."))
            .Column("isbn", SqlType.Text, c => c.NotNull().Comment("ISBN-13 identifier."))
            .Column("title", SqlType.Citext, c => c.NotNull().Comment("Title of the book."))
            .Column("author_id", SqlType.Text, c => c.NotNull().Comment("Author of the book."))
            .Column("published_on", SqlType.Date, c => c.Comment("Publication date."))
            .Column("copies_total", SqlType.Int, c => c.NotNull().Default("1").Comment("Total number of physical copies held."))
            .Index("uc_books_isbn", ["isbn"], i => i.Unique())
            .ForeignKey("fk_books_author", ["author_id"], "library", "authors", ["id"], _ => { })
        );

        public SchemaBuilder Members() => schema.Table("members", t => t
            .Comment("Registered library members who can borrow books.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("members_pkey").Comment("Primary key."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Member's full name."))
            .Column("email", SqlType.Citext, c => c.NotNull().Comment("Contact email address."))
            .Column("joined_at", SqlType.DateTimeOffset, c => c.NotNull().Comment("When the member joined the library."))
            .Index("uc_members_email", ["email"], i => i.Unique())
        );

        public SchemaBuilder Loans() => schema.Table("loans", t => t
            .Comment("Records of books loaned out to members.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("loans_pkey").Comment("Primary key."))
            .Column("book_id", SqlType.Text, c => c.NotNull().Comment("Book that was borrowed."))
            .Column("member_id", SqlType.Text, c => c.NotNull().Comment("Member who borrowed the book."))
            .Column("borrowed_at", SqlType.DateTimeOffset, c => c.NotNull().Comment("When the loan started."))
            .Column("due_at", SqlType.DateTimeOffset, c => c.NotNull().Comment("When the book is due back."))
            .Column("returned_at", SqlType.DateTimeOffset, c => c.Comment("When the book was returned, if at all."))
            .ForeignKey("fk_loans_book", ["book_id"], "library", "books", ["id"], _ => { })
            .ForeignKey("fk_loans_member", ["member_id"], "library", "members", ["id"], _ => { })
            .Index("ix_loans_member_id", ["member_id"], _ => { })
            .Index("ix_loans_open", ["book_id"], i => i.Where("(returned_at IS NULL)"))
        );
    }
}