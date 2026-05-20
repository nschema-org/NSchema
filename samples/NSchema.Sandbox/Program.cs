using Microsoft.Extensions.Logging;
using Npgsql;
using NSchema.Domain.Schema;
using NSchema.Fluent;
using NSchema.Migration;
using NSchema.Migration.Comparison;
using NSchema.Migration.Execution;
using NSchema.Postgres;

string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__sandbox")
    ?? throw new InvalidOperationException("Connection string not found in environment variables.");

var dataSource = NpgsqlDataSource.Create(connectionString);

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

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

var extractor = new PostgresSchemaExtractor(dataSource, "public");
var differ = new DefaultSchemaComparer();
var executor = new PostgresInstructionExecutor(loggerFactory.CreateLogger<PostgresInstructionExecutor>(), dataSource);
var migrator = new DefaultSchemaMigrator(loggerFactory.CreateLogger<DefaultSchemaMigrator>(), extractor, differ, executor, desired);

var plan = await migrator.Plan();
var options = new ExecutionOptions(DestructiveActionPolicy.Warn);
await migrator.Apply(plan, options);
