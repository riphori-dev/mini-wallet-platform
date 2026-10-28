using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MiniWallet.Infrastructure;

public static class DatabaseSetup
{
    public static async Task MigrateAsync(WalletDbContext db, CancellationToken cancellationToken = default)
    {
        // Session lock also protects EF8 history-table creation across competing migrators.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(810001)", (NpgsqlConnection)db.Database.GetDbConnection());
            await acquire.ExecuteNonQueryAsync(cancellationToken);
            try { await db.Database.MigrateAsync(cancellationToken); }
            finally
            {
                await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(810001)", (NpgsqlConnection)db.Database.GetDbConnection());
                await release.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}

