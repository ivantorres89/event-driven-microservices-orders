using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderProcess.Persistence.Impl.Migrations;

/// <summary>
/// Initial schema for Contoso OLTP.
/// </summary>
public partial class InitialContoso : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customer",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ExternalCustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                NationalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customer", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Product",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ExternalProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Product", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Order",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CustomerId = table.Column<long>(type: "bigint", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Order", x => x.Id);
                table.ForeignKey(
                    name: "FK_Order_Customer_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OrderItem",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OrderId = table.Column<long>(type: "bigint", nullable: false),
                ProductId = table.Column<long>(type: "bigint", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderItem", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrderItem_Order_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Order",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OrderItem_Product_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Product",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customer_ExternalCustomerId",
            table: "Customer",
            column: "ExternalCustomerId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Product_ExternalProductId",
            table: "Product",
            column: "ExternalProductId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Order_CorrelationId",
            table: "Order",
            column: "CorrelationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Order_CustomerId",
            table: "Order",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_OrderItem_OrderId",
            table: "OrderItem",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_OrderItem_ProductId",
            table: "OrderItem",
            column: "ProductId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrderItem");
        migrationBuilder.DropTable(name: "Order");
        migrationBuilder.DropTable(name: "Product");
        migrationBuilder.DropTable(name: "Customer");
    }
}
