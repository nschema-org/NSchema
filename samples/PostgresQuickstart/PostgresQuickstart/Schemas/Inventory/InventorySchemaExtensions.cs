using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace PostgresQuickstart.Schemas.Inventory;

internal static class InventorySchemaExtensions
{
    extension(SchemaBuilder schema)
    {
        public SchemaBuilder Suppliers() => schema.Table("suppliers", t => t
            .Comment("Companies that supply products to our warehouses.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("suppliers_pkey").Comment("Primary key."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Supplier's business name."))
            .Column("contact_email", SqlType.Citext, c => c.Comment("Primary contact email."))
            .Column("country", SqlType.Citext, c => c.NotNull().Comment("Country the supplier ships from."))
            .Index("uc_suppliers_name", ["name"], i => i.Unique())
        );

        public SchemaBuilder Products() => schema.Table("products", t => t
            .Comment("Products carried in inventory.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("products_pkey").Comment("Primary key."))
            .Column("sku", SqlType.Text, c => c.NotNull().Comment("Stock-keeping unit code."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Display name of the product."))
            .Column("description", SqlType.Citext, c => c.Comment("Long-form description."))
            .Column("supplier_id", SqlType.Text, c => c.NotNull().Comment("Supplier this product is sourced from."))
            .Column("unit_price_cents", SqlType.Int, c => c.NotNull().Default("0").Comment("Wholesale unit price in cents."))
            .Column("is_active", SqlType.Boolean, c => c.NotNull().Default("true").Comment("Whether the product is still being stocked."))
            .Index("uc_products_sku", ["sku"], i => i.Unique())
            .ForeignKey("fk_products_supplier", ["supplier_id"], "inventory", "suppliers", ["id"], _ => { })
        );

        public SchemaBuilder Warehouses() => schema.Table("warehouses", t => t
            .Comment("Physical warehouses that hold stock.")
            .Column("id", SqlType.Text, c => c.PrimaryKey("warehouses_pkey").Comment("Primary key."))
            .Column("name", SqlType.Citext, c => c.NotNull().Comment("Human-readable warehouse name."))
            .Column("city", SqlType.Citext, c => c.NotNull().Comment("City the warehouse is located in."))
            .Column("postcode", SqlType.Text, c => c.NotNull().Comment("Postal code."))
            .Index("uc_warehouses_name", ["name"], i => i.Unique())
        );

        public SchemaBuilder StockLevels() => schema.Table("stock_levels", t => t
            .Comment("Current on-hand quantity for each product at each warehouse.")
            .Column("product_id", SqlType.Text, c => c.NotNull().Comment("Product being stocked."))
            .Column("warehouse_id", SqlType.Text, c => c.NotNull().Comment("Warehouse holding the stock."))
            .Column("quantity_on_hand", SqlType.Int, c => c.NotNull().Default("0").Comment("Units currently on hand."))
            .Column("reorder_level", SqlType.Int, c => c.NotNull().Default("0").Comment("Trigger a reorder when on-hand falls to this level."))
            .Column("last_counted_at", SqlType.DateTimeOffset, c => c.Comment("When stock was last physically counted."))
            .PrimaryKey("stock_levels_pkey", ["product_id", "warehouse_id"])
            .ForeignKey("fk_stock_levels_product", ["product_id"], "inventory", "products", ["id"], _ => { })
            .ForeignKey("fk_stock_levels_warehouse", ["warehouse_id"], "inventory", "warehouses", ["id"], _ => { })
        );
    }
}