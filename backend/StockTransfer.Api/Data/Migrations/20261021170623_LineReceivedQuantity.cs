using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockTransfer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LineReceivedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "TransferLines",
                type: "integer",
                nullable: true);

            // Before this column existed, a receive always took in the full quantity.
            migrationBuilder.Sql("""
                UPDATE "TransferLines" AS l SET "ReceivedQuantity" = l."Quantity"
                FROM "Transfers" AS t
                WHERE t."Id" = l."TransferId" AND t."Status" = 'Received'
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferLine_ReceivedQuantity_Range",
                table: "TransferLines",
                sql: "\"ReceivedQuantity\" IS NULL OR (\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"Quantity\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferLine_ReceivedQuantity_Range",
                table: "TransferLines");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "TransferLines");
        }
    }
}
