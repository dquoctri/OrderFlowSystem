using Microsoft.EntityFrameworkCore;
using OrderFlow.Payments.Infrastructure.Persistence.Entities;

namespace OrderFlow.Payments.Infrastructure.Persistence;

public sealed class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orderflow_payments");
        modelBuilder.Entity<PaymentEntity>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(payment => payment.Id);
            entity.HasIndex(payment => payment.OrderId).IsUnique();
            entity.Property(payment => payment.Amount).HasPrecision(10, 2);
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
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
