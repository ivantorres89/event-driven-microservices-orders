using FluentMigrator;

namespace OrderProcess.DatabaseMigrations.Migrations;

[Migration(1, "Init schema (Customer, Product, Order, OrderItem) + timestamps + UpdatedAt triggers")]
public sealed class Migration0001_InitSchema : Migration
{
    private const string ScriptName = "OrderProcess.DatabaseMigrations.Sql.0001_InitSchema.sql";

    public override void Up()
    {
        Execute.EmbeddedScript(ScriptName);
    }

    public override void Down()
    {
        // Best-effort rollback for local/dev scenarios.
        Execute.Sql(@"
            IF OBJECT_ID(N'dbo.trg_OrderItem_SetUpdatedAt', N'TR') IS NOT NULL DROP TRIGGER dbo.trg_OrderItem_SetUpdatedAt;
            IF OBJECT_ID(N'dbo.trg_Order_SetUpdatedAt', N'TR') IS NOT NULL DROP TRIGGER dbo.trg_Order_SetUpdatedAt;
            IF OBJECT_ID(N'dbo.trg_Product_SetUpdatedAt', N'TR') IS NOT NULL DROP TRIGGER dbo.trg_Product_SetUpdatedAt;
            IF OBJECT_ID(N'dbo.trg_Customer_SetUpdatedAt', N'TR') IS NOT NULL DROP TRIGGER dbo.trg_Customer_SetUpdatedAt;

            IF OBJECT_ID(N'dbo.OrderItem', N'U') IS NOT NULL DROP TABLE dbo.OrderItem;
            IF OBJECT_ID(N'dbo.[Order]', N'U') IS NOT NULL DROP TABLE dbo.[Order];
            IF OBJECT_ID(N'dbo.Product', N'U') IS NOT NULL DROP TABLE dbo.Product;
            IF OBJECT_ID(N'dbo.Customer', N'U') IS NOT NULL DROP TABLE dbo.Customer;
        ");
    }
}
