using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniWallet.Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "PlayerRegistrationDeliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "PlayerRegistrationDeliveries",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "PlayerRegistrationDeliveries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "PlayerRegistrationDeliveries");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "PlayerRegistrationDeliveries");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "PlayerRegistrationDeliveries");
        }
    }
}
