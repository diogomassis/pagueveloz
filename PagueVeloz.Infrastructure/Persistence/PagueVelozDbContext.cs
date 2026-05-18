using Microsoft.EntityFrameworkCore;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class PagueVelozDbContext(DbContextOptions<PagueVelozDbContext> options) : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>(builder =>
        {
            builder.ToTable("accounts");
            builder.HasKey(account => account.AccountId);
            builder.Property(account => account.AccountId).HasColumnName("account_id").HasMaxLength(100);
            builder.Property(account => account.ClientId).HasColumnName("client_id").HasMaxLength(100).IsRequired();
            builder.Property(account => account.Balance).HasColumnName("balance");
            builder.Property(account => account.ReservedBalance).HasColumnName("reserved_balance");
            builder.Property(account => account.CreditLimit).HasColumnName("credit_limit");
            builder.Property(account => account.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<IdempotencyRecordEntity>(builder =>
        {
            builder.ToTable("idempotency_records");
            builder.HasKey(record => record.ReferenceId);
            builder.Property(record => record.ReferenceId).HasColumnName("reference_id").HasMaxLength(100);
            builder.Property(record => record.TransactionId).HasColumnName("transaction_id").HasMaxLength(150).IsRequired();
            builder.Property(record => record.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            builder.Property(record => record.Balance).HasColumnName("balance");
            builder.Property(record => record.ReservedBalance).HasColumnName("reserved_balance");
            builder.Property(record => record.AvailableBalance).HasColumnName("available_balance");
            builder.Property(record => record.Timestamp).HasColumnName("timestamp");
            builder.Property(record => record.ErrorMessage).HasColumnName("error_message");
        });
    }
}