using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderLineEntity> OrderLines => Set<OrderLineEntity>();
    public DbSet<OrderSagaStateEntity> OrderSagaStates => Set<OrderSagaStateEntity>();
    public DbSet<OrderSagaLogEntity> OrderSagaLog => Set<OrderSagaLogEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orderflow_orders");

        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.CustomerId).HasMaxLength(100).IsRequired();
            entity.Property(order => order.TotalAmount).HasPrecision(10, 2);
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(order => order.FailureStage).HasConversion<string>().HasMaxLength(30);
            entity.Property(order => order.FailureReason).HasMaxLength(500);
            entity.HasIndex(order => order.CustomerId);
            entity.HasMany(order => order.Lines).WithOne(line => line.Order).HasForeignKey(line => line.OrderId);
            entity.HasOne(order => order.SagaState).WithOne(state => state.Order).HasForeignKey<OrderSagaStateEntity>(state => state.OrderId);
        });

        modelBuilder.Entity<OrderSagaLogEntity>(entity =>
        {
            entity.ToTable("order_saga_log");
            entity.HasKey(log => log.Id);
            entity.HasIndex(log => new { log.OrderId, log.EventType }).IsUnique();
            entity.Property(log => log.EventType).HasMaxLength(60).IsRequired();
            entity.Property(log => log.Source).HasMaxLength(20).IsRequired();
            entity.Property(log => log.Detail).HasColumnType("jsonb");
        });

        modelBuilder.Entity<OrderLineEntity>(entity =>
        {
            entity.ToTable("order_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Sku).HasMaxLength(50).IsRequired();
            entity.Property(line => line.UnitPrice).HasPrecision(10, 2);
        });

        modelBuilder.Entity<OrderSagaStateEntity>(entity =>
        {
            entity.ToTable("order_saga_state");
            entity.HasKey(state => state.OrderId);
        });

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.HasIndex(message => message.EventId).IsUnique();
            entity.HasIndex(message => new { message.PublishedAt, message.LockExpiresAt });
            entity.Property(message => message.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<InboxMessageEntity>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(message => message.EventId);
        });
    }
}
