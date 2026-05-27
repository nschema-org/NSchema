using NSchema.Schema.Fluent;

namespace PostgresQuickstart.Schemas.Inventory;

public class InventorySchema : AbstractSchemaProvider
{
    public InventorySchema()
    {
        Schema("inventory", schema => schema
            .Comment("Schema for warehouse inventory tracking.")
            .Suppliers()
            .Products()
            .Warehouses()
            .StockLevels()
        );
    }
}