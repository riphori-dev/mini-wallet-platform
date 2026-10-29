using Microsoft.EntityFrameworkCore;
using MiniWallet.Users.Domain;
using MiniWallet.Users.Application;
using MiniWallet.Users.Domain.Entities;
using Npgsql;

namespace MiniWallet.Users.Infrastructure;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedDemoAsync(UsersDbContext db, IPasswordService passwords)
    {
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == "demo@miniwallet.test");
        if (existing is not null)
        {
            if (existing.Id != Guid.Parse("de000000-0000-4000-8000-000000000001"))
                throw new InvalidOperationException("Demo email belongs to another account.");
            return;
        }
        var demo = User.DevelopmentPlayer(EmailAddress.Parse("demo@miniwallet.test"), passwords.Hash("Local-demo-password-123!"));
        db.Users.Add(demo);
        db.RegistrationDeliveries.Add(new() { PlayerId = demo.Id, OccurredAt = DateTimeOffset.UtcNow });
        try { await db.SaveChangesAsync(); } // EF commits demo and its pending delivery atomically.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear(); // SaveChanges' own failed transaction has rolled back.
            var seeded = await db.Users.AsNoTracking().SingleAsync(x => x.Email == "demo@miniwallet.test");
            if (seeded.Id != demo.Id) throw new InvalidOperationException("Development demo identity conflict.");
        }
    }
    // Invoked only by the Development migrator, never ordinary HTTP startup.
    public static async Task SeedAdminAsync(UsersDbContext db, IPasswordService passwords, string email, string password)
    {
        var normalized = EmailAddress.Parse(email).Value;
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == normalized);
        if (existing is not null)
        {
            if (existing.Role != "admin") throw new InvalidOperationException("Configured development admin email belongs to a player.");
            return;
        }
        db.Users.Add(User.DevelopmentAdmin(EmailAddress.Parse(email), passwords.Hash(password)));
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var seeded = await db.Users.AsNoTracking().SingleAsync(x => x.Email == normalized);
            if (seeded.Role != "admin") throw new InvalidOperationException("Development admin identity conflict.");
        }
    }
}

