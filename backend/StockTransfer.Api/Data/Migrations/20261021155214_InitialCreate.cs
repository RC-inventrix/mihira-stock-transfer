using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StockTransfer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromBranchId = table.Column<int>(type: "integer", nullable: false),
                    ToBranchId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfer_DifferentBranches", "\"FromBranchId\" <> \"ToBranchId\"");
                    table.ForeignKey(
                        name: "FK_Transfers_Branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => new { x.BranchId, x.ProductId });
                    table.CheckConstraint("CK_Stock_Quantity_NonNegative", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_Stock_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stock_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransferId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferLines", x => x.Id);
                    table.CheckConstraint("CK_TransferLine_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_TransferLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferLines_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "Warehouse", "Warehouse" },
                    { 2, "Colombo", "Branch" },
                    { 3, "Kandy", "Branch" },
                    { 4, "Galle", "Branch" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Name", "Sku" },
                values: new object[,]
                {
                    { 1, "Rice 5kg", "RICE-5KG" },
                    { 2, "Red Dhal 1kg", "DHAL-1KG" },
                    { 3, "Coconut Oil 1L", "COIL-1L" },
                    { 4, "Ceylon Tea 400g", "TEA-400G" },
                    { 5, "Sugar 1kg", "SUGAR-1KG" }
                });

            migrationBuilder.InsertData(
                table: "Stock",
                columns: new[] { "BranchId", "ProductId", "Quantity" },
                values: new object[,]
                {
                    { 1, 1, 500 },
                    { 1, 2, 300 },
                    { 1, 3, 200 },
                    { 1, 4, 250 },
                    { 1, 5, 400 },
                    { 2, 1, 40 },
                    { 2, 2, 25 },
                    { 2, 3, 10 },
                    { 2, 4, 60 },
                    { 2, 5, 0 },
                    { 3, 1, 5 },
                    { 3, 2, 50 },
                    { 3, 3, 30 },
                    { 3, 4, 0 },
                    { 3, 5, 20 },
                    { 4, 1, 15 },
                    { 4, 2, 0 },
                    { 4, 3, 3 },
                    { 4, 4, 35 },
                    { 4, 5, 25 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Name",
                table: "Branches",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stock_ProductId",
                table: "Stock",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLines_ProductId",
                table: "TransferLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLines_TransferId_ProductId",
                table: "TransferLines",
                columns: new[] { "TransferId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FromBranchId",
                table: "Transfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_IdempotencyKey",
                table: "Transfers",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_Status",
                table: "Transfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ToBranchId",
                table: "Transfers",
                column: "ToBranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stock");

            migrationBuilder.DropTable(
                name: "TransferLines");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
