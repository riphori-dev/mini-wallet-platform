using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MiniWallet.Users.Infrastructure.Migrations;

// SQL-owned audit table: deliberately not an application entity or EF tracked aggregate.
[DbContext(typeof(MiniWallet.Users.Infrastructure.Users.UsersDbContext))]
[Migration("20261029070000_RequestAuditTrail")]
public sealed class RequestAuditTrail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE "AuditEvents" (
            "Id" uuid PRIMARY KEY,
            "CreatedAt" timestamp with time zone NOT NULL,
            "Record" jsonb NOT NULL
        );
        CREATE INDEX "IX_AuditEvents_CreatedAt" ON "AuditEvents" ("CreatedAt");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AuditEvents");
}
