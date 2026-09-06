using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;

namespace OrderFlow.Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<StockItemEntity> StockItems => Set<StockItemEntity>();
    public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orderflow_inventory");
        modelBuilder.Entity<StockItemEntity>(entity =>
        {
            entity.ToTable("stock_items");
            entity.HasKey(item => item.Sku);
            entity.Property(item => item.Sku).HasMaxLength(50);
            entity.HasData(
                new StockItemEntity { Sku = "WIDGET-01", QuantityOnHand = 10, QuantityReserved = 0 },
                new StockItemEntity { Sku = "WIDGET-02", QuantityOnHand = 5, QuantityReserved = 0 });
        });
        modelBuilder.Entity<ReservationEntity>(entity =>
        {
            entity.ToTable("reservations");
            entity.HasKey(reservation => reservation.Id);
            entity.Property(reservation => reservation.Sku).HasMaxLength(50).IsRequired();
            entity.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(reservation => new { reservation.OrderId, reservation.Sku }).IsUnique();
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
