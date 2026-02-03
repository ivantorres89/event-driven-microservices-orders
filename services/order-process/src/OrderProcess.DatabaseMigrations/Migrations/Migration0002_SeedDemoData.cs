using FluentMigrator;

namespace OrderProcess.DatabaseMigrations.Migrations;

[Migration(2, "Seed demo data (145 products, 10 customers, 20 orders + items)")]
public sealed class Migration0002_SeedDemoData : Migration
{
    private const string ScriptName = "OrderProcess.DatabaseMigrations.Sql.0002_SeedDemoData.sql";

    public override void Up()
    {
        Execute.EmbeddedScript(ScriptName);
    }

    public override void Down()
    {
        // Best-effort cleanup for local/dev scenarios.
        Execute.Sql(@"
            -- Delete orders/items for seeded customers
            DELETE oi
            FROM dbo.OrderItem oi
            INNER JOIN dbo.[Order] o ON o.Id = oi.OrderId
            INNER JOIN dbo.Customer c ON c.Id = o.CustomerId
            WHERE c.ExternalCustomerId LIKE N'CUST-%';

            DELETE o
            FROM dbo.[Order] o
            INNER JOIN dbo.Customer c ON c.Id = o.CustomerId
            WHERE c.ExternalCustomerId LIKE N'CUST-%';

            DELETE FROM dbo.Customer WHERE ExternalCustomerId LIKE N'CUST-%';
            DELETE FROM dbo.Product WHERE Vendor IN (N'Microsoft', N'AWS', N'Cisco', N'CompTIA', N'Google', N'ISC2', N'ITIL', N'Pearson', N'Terraform', N'Databricks');
        ");
    }
}
