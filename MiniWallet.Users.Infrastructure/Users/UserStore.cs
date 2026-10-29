using Microsoft.EntityFrameworkCore;
using MiniWallet.Users.Application;
using MiniWallet.Users.Domain.Entities;
using Npgsql;

namespace MiniWallet.Users.Infrastructure.Users;

public sealed class UserStore(UsersDbContext db) : IUserStore
{
    public async Task<bool> RegisterAsync(User user, DateTimeOffset occurredAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Users.Add(user);
        db.RegistrationDeliveries.Add(new() { PlayerId = user.Id, OccurredAt = occurredAt });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_Users_Email" })
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email, ct);

    public async Task<bool> VerifyAsync(string tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"VerificationTokenHash\" = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (user is null || !user.Verify(tokenHash, now)) return false;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> ReplaceVerificationAsync(Guid userId, string hash, DateTimeOffset expiresAt,
        DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").SingleAsync(ct);
        if (user.EmailVerified) return false;
        user.SetVerification(hash, expiresAt, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}

