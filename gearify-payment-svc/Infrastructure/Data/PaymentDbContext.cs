using Gearify.PaymentService.Domain.Entities;
using Gearify.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Gearify.PaymentService.Infrastructure.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentLedgerEntry> PaymentLedgerEntries => Set<PaymentLedgerEntry>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureOutboxMessage(modelBuilder);
        ConfigurePaymentTransaction(modelBuilder);
        ConfigurePaymentLedgerEntry(modelBuilder);
        ConfigureRefund(modelBuilder);
    }

    private static void ConfigureOutboxMessage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.EventType)
                .HasColumnName("event_type")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.TopicArn)
                .HasColumnName("topic_arn")
                .HasMaxLength(512)
                .IsRequired();

            entity.Property(e => e.Payload)
                .HasColumnName("payload")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(e => e.MessageAttributes)
                .HasColumnName("message_attributes")
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.PublishedAt)
                .HasColumnName("published_at");

            entity.Property(e => e.RetryCount)
                .HasColumnName("retry_count")
                .HasDefaultValue(0);

            entity.Property(e => e.NextRetryAt)
                .HasColumnName("next_retry_at");

            entity.Property(e => e.LastError)
                .HasColumnName("last_error");

            // Index for polling unpublished messages
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_outbox_unpublished")
                .HasFilter("published_at IS NULL");

            // Index for cleanup of old published messages
            entity.HasIndex(e => e.PublishedAt)
                .HasDatabaseName("idx_outbox_published_at")
                .HasFilter("published_at IS NOT NULL");
        });
    }

    private static void ConfigurePaymentTransaction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.OrderId)
                .HasColumnName("order_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Provider)
                .HasColumnName("provider")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ProviderTransactionId)
                .HasColumnName("provider_transaction_id")
                .HasMaxLength(255);

            entity.Property(e => e.IdempotencyKey)
                .HasColumnName("idempotency_key")
                .HasMaxLength(255);

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("ix_payment_transactions_tenant_id");

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("ix_payment_transactions_order_id");

            entity.HasIndex(e => e.IdempotencyKey)
                .HasDatabaseName("ix_payment_transactions_idempotency_key");

            entity.HasIndex(e => new { e.TenantId, e.CreatedAt })
                .HasDatabaseName("ix_payment_transactions_tenant_created");
        });
    }

    private static void ConfigurePaymentLedgerEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentLedgerEntry>(entity =>
        {
            entity.ToTable("payment_ledger");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseIdentityAlwaysColumn();

            entity.Property(e => e.TransactionId)
                .HasColumnName("transaction_id")
                .IsRequired();

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.AccountType)
                .HasColumnName("account_type")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD")
                .IsRequired();

            entity.Property(e => e.EntryTime)
                .HasColumnName("entry_time")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(500);

            // Foreign key
            entity.HasOne(e => e.Transaction)
                .WithMany(t => t.LedgerEntries)
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index
            entity.HasIndex(e => e.TransactionId)
                .HasDatabaseName("ix_payment_ledger_transaction_id");
        });
    }

    private static void ConfigureRefund(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Refund>(entity =>
        {
            entity.ToTable("refunds");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.TransactionId)
                .HasColumnName("transaction_id")
                .IsRequired();

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ProviderRefundId)
                .HasColumnName("provider_refund_id")
                .HasMaxLength(255);

            entity.Property(e => e.Reason)
                .HasColumnName("reason")
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            // Foreign key
            entity.HasOne(e => e.Transaction)
                .WithMany(t => t.Refunds)
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index
            entity.HasIndex(e => e.TransactionId)
                .HasDatabaseName("ix_refunds_transaction_id");

            entity.HasIndex(e => new { e.TenantId, e.CreatedAt })
                .HasDatabaseName("ix_refunds_tenant_created");
        });
    }
}
