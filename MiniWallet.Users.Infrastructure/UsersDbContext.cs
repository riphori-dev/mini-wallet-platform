using Microsoft.EntityFrameworkCore;
using MiniWallet.Users.Domain;

namespace MiniWallet.Users.Infrastructure;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PlayerRegistrationDelivery> RegistrationDeliveries => Set<PlayerRegistrationDelivery>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<User>();
        user.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('player','admin')");
            table.HasCheckConstraint("CK_Users_Email", "\"Email\" = lower(btrim(\"Email\"))");
        });
        user.HasKey(x => x.Id);
        user.Property(x => x.Email).HasMaxLength(254).IsRequired();
        user.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_Users_Email");
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.Role).HasMaxLength(16).IsRequired();
        user.Property(x => x.VerificationTokenHash).HasMaxLength(64);
        user.HasIndex(x => x.VerificationTokenHash).IsUnique();
        var delivery = model.Entity<PlayerRegistrationDelivery>();
        delivery.ToTable("PlayerRegistrationDeliveries");
        delivery.HasKey(x => x.EventId);
        delivery.HasIndex(x => x.PlayerId).IsUnique();
        delivery.Property(x => x.LastError).HasMaxLength(512);
        delivery.HasOne<User>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlayerRegistrationDelivery
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}


