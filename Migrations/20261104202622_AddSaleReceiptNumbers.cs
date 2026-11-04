using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleReceiptNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptSequence",
                table: "Sales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BusinessDate_ReceiptSequence",
                table: "Sales",
                columns: new[] { "BusinessDate", "ReceiptSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ReceiptNumber",
                table: "Sales",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_BusinessDate_ReceiptSequence",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ReceiptNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReceiptSequence",
                table: "Sales");
        }
    }
}
