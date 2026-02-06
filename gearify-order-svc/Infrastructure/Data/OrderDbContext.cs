using Gearify.OrderService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gearify.OrderService.Infrastructure.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.OrderNumber)
                .HasColumnName("order_number")
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(e => e.OrderNumber)
                .IsUnique();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(30)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.Subtotal)
                .HasColumnName("subtotal")
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(e => e.TaxAmount)
                .HasColumnName("tax_amount")
                .HasPrecision(12, 2)
                .HasDefaultValue(0);

            entity.Property(e => e.ShippingAmount)
                .HasColumnName("shipping_amount")
                .HasPrecision(12, 2)
                .HasDefaultValue(0);

            entity.Property(e => e.DiscountAmount)
                .HasColumnName("discount_amount")
                .HasPrecision(12, 2)
                .HasDefaultValue(0);

            entity.Property(e => e.TotalAmount)
                .HasColumnName("total_amount")
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD");

            // Address fields
            entity.Property(e => e.ShippingAddressId)
                .HasColumnName("shipping_address_id")
                .HasMaxLength(100);

            entity.Property(e => e.ShippingAddress)
                .HasColumnName("shipping_address")
                .HasColumnType("jsonb");

            entity.Property(e => e.BillingAddressId)
                .HasColumnName("billing_address_id")
                .HasMaxLength(100);

            entity.Property(e => e.BillingAddress)
                .HasColumnName("billing_address")
                .HasColumnType("jsonb");

            // Payment fields
            entity.Property(e => e.PaymentId)
                .HasColumnName("payment_id");

            entity.Property(e => e.PaymentStatus)
                .HasColumnName("payment_status")
                .HasMaxLength(30);

            // Shipping fields
            entity.Property(e => e.ShipmentId)
                .HasColumnName("shipment_id");

            entity.Property(e => e.ShippingStatus)
                .HasColumnName("shipping_status")
                .HasMaxLength(30);

            // Saga fields
            entity.Property(e => e.SagaState)
                .HasColumnName("saga_state")
                .HasMaxLength(50)
                .HasConversion<string>()
                .HasDefaultValue(SagaState.Created);

            entity.Property(e => e.SagaStep)
                .HasColumnName("saga_step")
                .HasMaxLength(50);

            entity.Property(e => e.SagaError)
                .HasColumnName("saga_error");

            // Timestamps
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.CancelledAt)
                .HasColumnName("cancelled_at");

            // Cancellation tracking
            entity.Property(e => e.CancellationReason)
                .HasColumnName("cancellation_reason")
                .HasMaxLength(500);

            entity.Property(e => e.CancellationRequestedBy)
                .HasColumnName("cancellation_requested_by")
                .HasMaxLength(100);

            entity.Property(e => e.CancellationRequestedAt)
                .HasColumnName("cancellation_requested_at");

            // Metadata
            entity.Property(e => e.Notes)
                .HasColumnName("notes");

            entity.Property(e => e.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            // Indexes
            entity.HasIndex(e => e.TenantId).HasDatabaseName("idx_orders_tenant_id");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_orders_user_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_orders_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_orders_created_at").IsDescending();
            entity.HasIndex(e => e.SagaState).HasDatabaseName("idx_orders_saga_state");

            // Relationships
            entity.HasMany(e => e.Items)
                .WithOne(e => e.Order)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.StatusHistory)
                .WithOne(e => e.Order)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderItem configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.OrderId)
                .HasColumnName("order_id")
                .IsRequired();

            entity.Property(e => e.ProductId)
                .HasColumnName("product_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ProductSku)
                .HasColumnName("product_sku")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ProductName)
                .HasColumnName("product_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.ProductImageUrl)
                .HasColumnName("product_image_url");

            entity.Property(e => e.Quantity)
                .HasColumnName("quantity")
                .IsRequired();

            entity.Property(e => e.UnitPrice)
                .HasColumnName("unit_price")
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(e => e.DiscountAmount)
                .HasColumnName("discount_amount")
                .HasPrecision(12, 2)
                .HasDefaultValue(0);

            entity.Property(e => e.TotalPrice)
                .HasColumnName("total_price")
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(e => e.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            // Indexes
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_order_items_order_id");
            entity.HasIndex(e => e.ProductId).HasDatabaseName("idx_order_items_product_id");
        });

        // OrderStatusHistory configuration
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("order_status_history");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.OrderId)
                .HasColumnName("order_id")
                .IsRequired();

            entity.Property(e => e.FromStatus)
                .HasColumnName("from_status")
                .HasMaxLength(30);

            entity.Property(e => e.ToStatus)
                .HasColumnName("to_status")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.ChangedBy)
                .HasColumnName("changed_by")
                .HasMaxLength(100);

            entity.Property(e => e.Reason)
                .HasColumnName("reason");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            // Indexes
            entity.HasIndex(e => e.OrderId).HasDatabaseName("idx_order_status_history_order_id");
        });
    }
}
