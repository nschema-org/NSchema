using NSchema.Model.Columns;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

public sealed class NsqlParserTableTests
{
    private static Table ParseTable(string body, string qualifiedName = "app.users")
    {
        var schema = new TestNsqlParser($"CREATE TABLE {qualifiedName} ({body});").Parse().Database.Schemas.Single();
        return schema.Tables.Single();
    }

    private static Column Column(string body) => ParseTable(body).Columns.Single();

    // -------------------------------------------------------------------------
    // Columns
    // -------------------------------------------------------------------------

    [Fact]
    public void Column_NotNull_IsNotNullable()
    {
        // Act
        var column = Column("id int NOT NULL");

        // Assert
        column.Name.ShouldBe("id");
        column.Type.ShouldBe(SqlType.Int);
        column.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Column_NoNullability_DefaultsToNullable()
        => Column("email text").IsNullable.ShouldBeTrue();

    [Fact]
    public void Column_ExplicitNull_IsNullable()
        => Column("note text NULL").IsNullable.ShouldBeTrue();

    [Theory]
    [InlineData("name varchar(255)", "varchar", 255)]
    [InlineData("code char(8)", "char", 8)]
    public void Column_ParameterisedType_CapturesLength(string body, string name, int length)
    {
        // Act
        var type = Column(body).Type;

        // Assert
        type.Name.ShouldBe(name);
        type.Length.ShouldBe(length);
    }

    [Fact]
    public void Column_DecimalType_CapturesPrecisionAndScale()
        => Column("amount decimal(18, 2)").Type.ShouldBe(SqlType.Decimal(18, 2));

    [Fact]
    public void Column_IntegerSpelling_AliasesToCanonicalInt()
        => Column("balance integer NOT NULL").Type.ShouldBe(SqlType.Int);

    [Fact]
    public void Column_UnknownType_BecomesCustom()
        => Column("data jsonb").Type.ShouldBe(SqlType.Custom("jsonb"));

    [Fact]
    public void Column_SchemaQualifiedType_CapturesTheQualifierStructurally()
        => Column("state app.status").Type.ShouldBe(SqlType.Custom("app", "status"));

    [Fact]
    public void Column_SchemaQualifiedType_WithConstraint_Parses()
    {
        // Act
        var column = Column("state app.status NOT NULL");

        // Assert
        column.Type.ShouldBe(SqlType.Custom("app", "status"));
        column.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Column_DefaultLiteral_CapturesExpression()
        => Column("quantity int NOT NULL DEFAULT 1").DefaultExpression.ShouldBe("1");

    [Fact]
    public void Column_DefaultFunctionWithCommas_CapturesWholeExpression()
        => Column("score int DEFAULT coalesce(a, b)").DefaultExpression.ShouldBe("coalesce(a, b)");

    [Fact]
    public void Column_DefaultContainingRenamed_IsJustExpressionText()
        // RENAMED FROM is no longer a clause, so nothing terminates a default early; it reads to the comma.
        => Column("at int DEFAULT renamed_at").DefaultExpression.ShouldBe("renamed_at");

    [Fact]
    public void Column_BareIdentity_SetsIdentityWithoutOptions()
    {
        // Act
        var column = Column("id bigint IDENTITY");

        // Assert
        column.IsIdentity.ShouldBeTrue();
        column.IdentityOptions.ShouldBeNull();
    }

    [Fact]
    public void Column_IdentityWithOptions_CapturesThem()
    {
        // Act
        var column = Column("id bigint IDENTITY (START 1, INCREMENT 2, MINVALUE 0)");

        // Assert
        column.IsIdentity.ShouldBeTrue();
        column.IdentityOptions.ShouldBe(new IdentityOptions(StartWith: 1, MinValue: 0, IncrementBy: 2));
    }

    [Fact]
    public void Column_DocComment_BecomesColumnComment()
        => ParseTable("--- The primary contact address.\n  email text").Columns.Single().Comment.ShouldBe("The primary contact address.");

    // -------------------------------------------------------------------------
    // Constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Constraint_PrimaryKey_IsCaptured()
    {
        // Act
        var pk = ParseTable("id int NOT NULL, CONSTRAINT users_pkey PRIMARY KEY (id)").PrimaryKey;

        // Assert
        pk.ShouldNotBeNull();
        pk!.Name.ShouldBe("users_pkey");
        pk.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public void Constraint_CompositePrimaryKey_CapturesAllColumns()
        => ParseTable("a int, b int, CONSTRAINT pk PRIMARY KEY (a, b)").PrimaryKey!.ColumnNames.ShouldBe(["a", "b"]);

    [Fact]
    public void Constraint_TwoPrimaryKeys_Throws()
        => Should.Throw<NsqlSyntaxException>(() => ParseTable("id int, CONSTRAINT pk1 PRIMARY KEY (id), CONSTRAINT pk2 PRIMARY KEY (id)"))
            .Message.ShouldContain("only one primary key");

    [Fact]
    public void Constraint_ForeignKey_CapturesReferenceAndActions()
    {
        // Arrange
        var fk = ParseTable(
            "user_id int, CONSTRAINT fk_user FOREIGN KEY (user_id) REFERENCES app.users (id) ON DELETE CASCADE ON UPDATE SET NULL")

            // Act
            .ForeignKeys.Single();

        // Assert
        fk.Name.ShouldBe("fk_user");
        fk.ColumnNames.ShouldBe(["user_id"]);
        fk.References.Schema.ShouldBe("app");
        fk.References.Name.ShouldBe("users");
        fk.ReferencedColumnNames.ShouldBe(["id"]);
        fk.OnDelete.ShouldBe(ReferentialAction.Cascade);
        fk.OnUpdate.ShouldBe(ReferentialAction.SetNull);
    }

    [Fact]
    public void Constraint_ForeignKeyWithoutActions_DefaultsToNoAction()
    {
        // Act
        var fk = ParseTable("user_id int, CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES app.users (id)").ForeignKeys.Single();

        // Assert
        fk.OnDelete.ShouldBe(ReferentialAction.NoAction);
        fk.OnUpdate.ShouldBe(ReferentialAction.NoAction);
    }

    [Fact]
    public void Constraint_ForeignKeySetDefault_IsParsed()
        => ParseTable("user_id int, CONSTRAINT fk FOREIGN KEY (user_id) REFERENCES app.users (id) ON DELETE SET DEFAULT")
            .ForeignKeys.Single().OnDelete.ShouldBe(ReferentialAction.SetDefault);

    [Fact]
    public void Constraint_Unique_IsCaptured()
    {
        // Act
        var unique = ParseTable("email text, CONSTRAINT users_email_uq UNIQUE (email)").UniqueConstraints.Single();

        // Assert
        unique.Name.ShouldBe("users_email_uq");
        unique.ColumnNames.ShouldBe(["email"]);
    }

    [Fact]
    public void Constraint_Check_CapturesOpaqueExpression()
    {
        // Act
        var check = ParseTable("age int, CONSTRAINT users_age_chk CHECK (age >= 0 AND age < 150)").CheckConstraints.Single();

        // Assert
        check.Name.ShouldBe("users_age_chk");
        check.Expression.ShouldBe("age >= 0 AND age < 150");
    }

    [Fact]
    public void Constraint_DocComment_BecomesConstraintComment()
        => ParseTable("age int, --- Must be non-negative.\n CONSTRAINT chk CHECK (age >= 0)")
            .CheckConstraints.Single().Comment.ShouldBe("Must be non-negative.");

    // -------------------------------------------------------------------------
    // Indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Index_Plain_IsCaptured()
    {
        // Act
        var index = ParseTable("email text, INDEX ix_email (email)").Indexes.Single();

        // Assert
        index.Name.ShouldBe("ix_email");
        index.Columns.Select(c => c.Column?.Value).ShouldBe(["email"]);
        index.IsUnique.ShouldBeFalse();
        index.Predicate.ShouldBeNull();
    }

    [Fact]
    public void Index_Unique_SetsIsUnique()
        => ParseTable("email text, UNIQUE INDEX ux_email (email)").Indexes.Single().IsUnique.ShouldBeTrue();

    [Fact]
    public void Index_Partial_CapturesPredicate()
        => ParseTable("email text, INDEX ix (email) WHERE (deleted_at IS NULL)").Indexes.Single().Predicate.ShouldBe("deleted_at IS NULL");

    // -------------------------------------------------------------------------
    // Grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Grant_TablePrivileges_AttachToTable()
    {
        // Arrange
        var schema = new TestNsqlParser(
            """
            CREATE TABLE app.users (id int);
            GRANT SELECT, INSERT ON app.users TO readers;
            """).Parse().Database.Schemas.Single();

        // Act
        var grant = schema.Tables.Single().Grants.Single();

        // Assert
        grant.Role.ShouldBe("readers");
        grant.Privileges.ShouldBe(TablePrivilege.Select | TablePrivilege.Insert);
    }

    [Fact]
    public void Grant_BeforeTable_IsResolvedAtBuild()
    {
        // Grants are order-independent: a grant may precede the CREATE TABLE it targets.
        var schema = new TestNsqlParser(
            """
            GRANT SELECT ON app.users TO readers;
            CREATE TABLE app.users (id int);
            """).Parse().Database.Schemas.Single();
        schema.Tables.Single().Grants.Single().Privileges.ShouldBe(TablePrivilege.Select);
    }

    [Fact]
    public void Grant_SchemaUsage_AttachesToSchema()
    {
        var schema = new TestNsqlParser("CREATE SCHEMA app; GRANT USAGE ON SCHEMA app TO app_role;").Parse().Database.Schemas.Single();
        schema.Grants.Single().Role.ShouldBe("app_role");
    }

    [Fact]
    public void Grant_UnknownTable_FailsTheRead()
        => new TestNsqlParser("GRANT SELECT ON app.ghost TO readers;").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("unknown table");

    [Fact]
    public void Grant_UnknownPrivilege_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE TABLE app.t (id int); GRANT TRUNCATE ON app.t TO r;").Parse().Database)
            .Message.ShouldContain("privilege");

    // -------------------------------------------------------------------------
    // Whole-table integration
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_RichTable_AssemblesEveryMember()
    {
        var table = new TestNsqlParser(
            """
            --- Line items for an order.
            CREATE TABLE shop.order_items (
              order_id    int           NOT NULL,
              product_id  int           NOT NULL,
              quantity    int           NOT NULL DEFAULT 1,
              note        text,

              CONSTRAINT order_items_pkey PRIMARY KEY (order_id, product_id),
              CONSTRAINT fk_order FOREIGN KEY (order_id) REFERENCES shop.orders (id) ON DELETE CASCADE,
              CONSTRAINT chk_qty CHECK (quantity > 0),

              INDEX ix_product (product_id),
              UNIQUE INDEX ux_note (note) WHERE (note IS NOT NULL)
            );
            """).Parse().Database.Schemas.Single().Tables.Single();

        table.Name.ShouldBe("order_items");
        table.Comment.ShouldBe("Line items for an order.");
        table.Columns.Select(c => c.Name).ShouldBe(["order_id", "product_id", "quantity", "note"]);
        table.Columns.Single(c => c.Name.Value.Equals("quantity")).DefaultExpression.ShouldBe("1");
        table.PrimaryKey!.ColumnNames.ShouldBe(["order_id", "product_id"]);
        table.ForeignKeys.Single().References.Name.ShouldBe("orders");
        table.CheckConstraints.Single().Expression.ShouldBe("quantity > 0");
        table.Indexes.Select(i => (i.Name.Value, i.IsUnique)).ShouldBe([("ix_product", false), ("ux_note", true)]);
        table.Indexes.Single(i => i.Name.Value.Equals("ux_note")).Predicate.ShouldBe("note IS NOT NULL");
    }
}
