using Microsoft.EntityFrameworkCore;
using MiniWallet.Domain;

namespace MiniWallet.Infrastructure;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<PlayerWallet> Wallets => Set<PlayerWallet>();
    public DbSet<PlayerRegistrationReceipt> RegistrationReceipts => Set<PlayerRegistrationReceipt>();
    public DbSet<FinancialEntry> Entries => Set<FinancialEntry>();
    public DbSet<TransactionOutcome> TransactionOutcomes => Set<TransactionOutcome>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var wallet = model.Entity<PlayerWallet>();
        wallet.ToTable("Wallets", table =>
        {
            table.HasCheckConstraint("CK_Wallets_Currency", "\"Currency\" IN ('USD','EUR','BTC')");
            table.HasCheckConstraint("CK_Wallets_Balance", "\"Balance\" >= 0 AND \"Balance\" <= 99999999999999999999.99999999");
            table.HasCheckConstraint("CK_Wallets_Scale", "(\"Currency\" = 'BTC' AND \"Balance\" = trunc(\"Balance\",8)) OR (\"Currency\" IN ('USD','EUR') AND \"Balance\" = trunc(\"Balance\",2))");
        });
        wallet.HasKey(x => x.Id);
        wallet.HasIndex(x => new { x.PlayerId, x.Currency }).IsUnique();
        wallet.Property(x => x.Currency).HasConversion(c => c.Code, c => Currency.Parse(c)).HasMaxLength(3).IsRequired();
        // Unconstrained numeric plus checks rejects excess monetary digits instead of typmod rounding.
        wallet.Property(x => x.Balance).HasColumnType("numeric");
        wallet.HasAlternateKey(x => new { x.Id, x.PlayerId, x.Currency });
        var entry = model.Entity<FinancialEntry>();
        entry.ToTable("FinancialEntries", table =>
        {
            table.HasCheckConstraint("CK_Entries_Type", "\"Type\" IN ('BET','WIN','DEV_FUNDING')");
            table.HasCheckConstraint("CK_Entries_Amount", "\"Amount\" > 0 AND \"Amount\" <= 99999999999999999999.99999999");
            table.HasCheckConstraint("CK_Entries_Balance", "\"BalanceAfter\" >= 0 AND \"BalanceAfter\" <= 99999999999999999999.99999999");
            table.HasCheckConstraint("CK_Entries_Scale", "(\"Currency\" = 'BTC' AND \"Amount\" = trunc(\"Amount\",8) AND \"BalanceAfter\" = trunc(\"BalanceAfter\",8)) OR (\"Currency\" IN ('USD','EUR') AND \"Amount\" = trunc(\"Amount\",2) AND \"BalanceAfter\" = trunc(\"BalanceAfter\",2))");
            table.HasCheckConstraint("CK_Entries_Bet", "(\"Type\" = 'WIN') = (\"OriginalBetId\" IS NOT NULL)");
        });
        entry.HasKey(x => x.Id);
        entry.Property(x => x.Currency).HasConversion(c => c.Code, c => Currency.Parse(c)).HasMaxLength(3);
        entry.Property(x => x.ProviderId).HasMaxLength(128);
        entry.Property(x => x.ExternalTransactionId).HasMaxLength(128);
        entry.Property(x => x.Type).HasMaxLength(16);
        entry.Property(x => x.Amount).HasColumnType("numeric");
        entry.Property(x => x.BalanceAfter).HasColumnType("numeric");
        entry.HasIndex(x => new { x.ProviderId, x.ExternalTransactionId }).IsUnique();
        entry.HasIndex(x => x.OriginalBetId).IsUnique().HasFilter("\"OriginalBetId\" IS NOT NULL");
        entry.HasIndex(x => new { x.PlayerId, x.Currency, x.CreatedAt, x.Id });
        entry.HasOne<FinancialEntry>().WithMany().HasForeignKey(x => x.OriginalBetId).OnDelete(DeleteBehavior.Restrict);
        entry.HasOne<PlayerWallet>().WithMany().HasForeignKey(x => new { x.WalletId, x.PlayerId, x.Currency })
            .HasPrincipalKey(x => new { x.Id, x.PlayerId, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        var outcome = model.Entity<TransactionOutcome>();
        outcome.ToTable("TransactionOutcomes", table =>
        {
            table.HasCheckConstraint("CK_Outcomes_Status", "\"Status\" IN (0,201,409)");
            table.HasCheckConstraint("CK_Outcomes_Amount", "\"Amount\" > 0 AND \"Amount\" <= 99999999999999999999.99999999");
            table.HasCheckConstraint("CK_Outcomes_Scale", "(\"Currency\" = 'BTC' AND \"Amount\" = trunc(\"Amount\",8)) OR (\"Currency\" IN ('USD','EUR') AND \"Amount\" = trunc(\"Amount\",2))");
            table.HasCheckConstraint("CK_Outcomes_Type", "(\"Type\" = 'BET' AND \"BetExternalTransactionId\" IS NULL) OR (\"Type\" = 'WIN' AND \"BetExternalTransactionId\" IS NOT NULL)");
        });
        outcome.HasKey(x => new { x.ProviderId, x.ExternalTransactionId });
        outcome.Property(x => x.ProviderId).HasMaxLength(128);
        outcome.Property(x => x.ExternalTransactionId).HasMaxLength(128);
        outcome.Property(x => x.BetExternalTransactionId).HasMaxLength(128);
        outcome.Property(x => x.Currency).HasMaxLength(3);
        outcome.Property(x => x.Type).HasMaxLength(16);
        outcome.Property(x => x.Amount).HasColumnType("numeric");
        var receipt = model.Entity<PlayerRegistrationReceipt>();
        receipt.ToTable("PlayerRegistrationReceipts");
        receipt.HasKey(x => x.EventId);
    }
}

public sealed class PlayerRegistrationReceipt
{
    public Guid EventId { get; set; }
    public Guid PlayerId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int Version { get; set; }
}

