using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderFlow.Orders.Infrastructure.Persistence;

#nullable disable

namespace OrderFlow.Orders.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OrdersDbContext))]
[Migration("202609050001_InitialCreate")]
public partial class InitialCreate : Migration
{
    private static readonly string[] PublishedLockIndexColumns = ["PublishedAt", "LockExpiresAt"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "orderflow_orders");

        migrationBuilder.CreateTable(
            name: "orders",
            schema: "orderflow_orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_orders", x => x.Id));

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: "orderflow_orders",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_inbox_messages", x => x.EventId));

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "orderflow_orders",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "text", nullable: false),
                Topic = table.Column<string>(type: "text", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockOwner = table.Column<string>(type: "text", nullable: true),
                LockExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_outbox_messages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "order_lines",
            schema: "orderflow_orders",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_lines", x => x.Id);
                table.ForeignKey("FK_order_lines_orders_OrderId", x => x.OrderId, principalTable: "orders", principalColumn: "Id", principalSchema: "orderflow_orders", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "order_saga_state",
            schema: "orderflow_orders",
            columns: table => new
            {
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                ReservationCompleted = table.Column<bool>(type: "boolean", nullable: false),
                PaymentCompleted = table.Column<bool>(type: "boolean", nullable: false),
                LastProcessedEventId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_saga_state", x => x.OrderId);
                table.ForeignKey("FK_order_saga_state_orders_OrderId", x => x.OrderId, principalTable: "orders", principalColumn: "Id", principalSchema: "orderflow_orders", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_orders_CustomerId", schema: "orderflow_orders", table: "orders", column: "CustomerId");
        migrationBuilder.CreateIndex(name: "IX_order_lines_OrderId", schema: "orderflow_orders", table: "order_lines", column: "OrderId");
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_EventId", schema: "orderflow_orders", table: "outbox_messages", column: "EventId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_PublishedAt_LockExpiresAt", schema: "orderflow_orders", table: "outbox_messages", columns: PublishedLockIndexColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inbox_messages", schema: "orderflow_orders");
        migrationBuilder.DropTable(name: "order_lines", schema: "orderflow_orders");
        migrationBuilder.DropTable(name: "order_saga_state", schema: "orderflow_orders");
        migrationBuilder.DropTable(name: "outbox_messages", schema: "orderflow_orders");
        migrationBuilder.DropTable(name: "orders", schema: "orderflow_orders");
    }
}
