using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniWallet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlayerProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerRegistrationReceipts",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRegistrationReceipts", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.CheckConstraint("CK_Wallets_Balance", "\"Balance\" >= 0 AND \"Balance\" <= 99999999999999999999.99999999");
                    table.CheckConstraint("CK_Wallets_Currency", "\"Currency\" IN ('USD','EUR','BTC')");
                    table.CheckConstraint("CK_Wallets_Scale", "\"Currency\" = 'BTC' OR \"Balance\" = trunc(\"Balance\", 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_PlayerId_Currency",
                table: "Wallets",
                columns: new[] { "PlayerId", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRegistrationReceipts");

            migrationBuilder.DropTable(
                name: "Wallets");
        }
    }
}
