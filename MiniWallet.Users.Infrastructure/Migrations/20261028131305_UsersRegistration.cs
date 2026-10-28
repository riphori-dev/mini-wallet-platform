using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniWallet.Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsersRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VerificationExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Email", "\"Email\" = lower(btrim(\"Email\"))");
                    table.CheckConstraint("CK_Users_Role", "\"Role\" IN ('player','admin')");
                });

            migrationBuilder.CreateTable(
                name: "PlayerRegistrationDeliveries",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRegistrationDeliveries", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_PlayerRegistrationDeliveries_Users_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRegistrationDeliveries_PlayerId",
                table: "PlayerRegistrationDeliveries",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_VerificationTokenHash",
                table: "Users",
                column: "VerificationTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRegistrationDeliveries");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
