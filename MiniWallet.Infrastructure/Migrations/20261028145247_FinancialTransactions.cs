using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniWallet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Wallets",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Wallets_Id_PlayerId_Currency",
                table: "Wallets",
                columns: new[] { "Id", "PlayerId", "Currency" });

            migrationBuilder.CreateTable(
                name: "FinancialEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric", nullable: false),
                    OriginalBetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEntries", x => x.Id);
                    table.CheckConstraint("CK_Entries_Amount", "\"Amount\" > 0 AND \"Amount\" <= 99999999999999999999.99999999");
                    table.CheckConstraint("CK_Entries_Balance", "\"BalanceAfter\" >= 0 AND \"BalanceAfter\" <= 99999999999999999999.99999999");
                    table.CheckConstraint("CK_Entries_Bet", "(\"Type\" = 'WIN') = (\"OriginalBetId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Entries_Scale", "(\"Currency\" = 'BTC' AND \"Amount\" = trunc(\"Amount\",8) AND \"BalanceAfter\" = trunc(\"BalanceAfter\",8)) OR (\"Currency\" IN ('USD','EUR') AND \"Amount\" = trunc(\"Amount\",2) AND \"BalanceAfter\" = trunc(\"BalanceAfter\",2))");
                    table.CheckConstraint("CK_Entries_Type", "\"Type\" IN ('BET','WIN','DEV_FUNDING')");
                    table.ForeignKey(
                        name: "FK_FinancialEntries_FinancialEntries_OriginalBetId",
                        column: x => x.OriginalBetId,
                        principalTable: "FinancialEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialEntries_Wallets_WalletId_PlayerId_Currency",
                        columns: x => new { x.WalletId, x.PlayerId, x.Currency },
                        principalTable: "Wallets",
                        principalColumns: new[] { "Id", "PlayerId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionOutcomes",
                columns: table => new
                {
                    ProviderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    BetExternalTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionOutcomes", x => new { x.ProviderId, x.ExternalTransactionId });
                    table.CheckConstraint("CK_Outcomes_Amount", "\"Amount\" > 0 AND \"Amount\" <= 99999999999999999999.99999999");
                    table.CheckConstraint("CK_Outcomes_Scale", "(\"Currency\" = 'BTC' AND \"Amount\" = trunc(\"Amount\",8)) OR (\"Currency\" IN ('USD','EUR') AND \"Amount\" = trunc(\"Amount\",2))");
                    table.CheckConstraint("CK_Outcomes_Status", "\"Status\" IN (0,201,409)");
                    table.CheckConstraint("CK_Outcomes_Type", "(\"Type\" = 'BET' AND \"BetExternalTransactionId\" IS NULL) OR (\"Type\" = 'WIN' AND \"BetExternalTransactionId\" IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEntries_OriginalBetId",
                table: "FinancialEntries",
                column: "OriginalBetId",
                unique: true,
                filter: "\"OriginalBetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEntries_PlayerId_Currency_CreatedAt_Id",
                table: "FinancialEntries",
                columns: new[] { "PlayerId", "Currency", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEntries_ProviderId_ExternalTransactionId",
                table: "FinancialEntries",
                columns: new[] { "ProviderId", "ExternalTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEntries_WalletId_PlayerId_Currency",
                table: "FinancialEntries",
                columns: new[] { "WalletId", "PlayerId", "Currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialEntries");

            migrationBuilder.DropTable(
                name: "TransactionOutcomes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Wallets_Id_PlayerId_Currency",
                table: "Wallets");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Wallets",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
