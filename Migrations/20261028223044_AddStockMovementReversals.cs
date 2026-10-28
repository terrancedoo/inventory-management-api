using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "StockMovements",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesMovementId",
                table: "StockMovements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReversesMovementId",
                table: "StockMovements",
                column: "ReversesMovementId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StockMovements_ReversesMovementId",
                table: "StockMovements",
                column: "ReversesMovementId",
                principalTable: "StockMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StockMovements_ReversesMovementId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ReversesMovementId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReversesMovementId",
                table: "StockMovements");
        }
    }
}
