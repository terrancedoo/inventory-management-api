using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExcludedAmount",
                table: "Sales",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SaleAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    LadyNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AllocatedEligibleSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AllocatedBottleCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AllocatedBack = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleAllocations_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleAllocations_SaleId_Position",
                table: "SaleAllocations",
                columns: new[] { "SaleId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleAllocations");

            migrationBuilder.DropColumn(
                name: "ExcludedAmount",
                table: "Sales");
        }
    }
}
