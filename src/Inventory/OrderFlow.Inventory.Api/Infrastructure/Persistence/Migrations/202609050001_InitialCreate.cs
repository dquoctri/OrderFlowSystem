using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderFlow.Inventory.Infrastructure.Persistence;

#nullable disable

namespace OrderFlow.Inventory.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("202609050001_InitialCreate")]
public partial class InitialCreate : Migration
{
    private static readonly string[] StockColumns = ["Sku", "QuantityOnHand", "QuantityReserved"];
    private static readonly string[] StockColumnTypes = ["character varying(50)", "integer", "integer"];
    private static readonly string[] ReservationIndexColumns = ["OrderId", "Sku"];
    private static readonly string[] PublishedLockIndexColumns = ["PublishedAt", "LockExpiresAt"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "orderflow_inventory");

        migrationBuilder.CreateTable(
            name: "stock_items",
            schema: "orderflow_inventory",
            columns: table => new
            {
                Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                QuantityReserved = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_stock_items", x => x.Sku));

        migrationBuilder.CreateTable(
            name: "reservations",
            schema: "orderflow_inventory",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_reservations", x => x.Id));

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: "orderflow_inventory",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_inbox_messages", x => x.EventId));

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "orderflow_inventory",
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

        migrationBuilder.InsertData(
            table: "stock_items",
            columns: StockColumns,
            columnTypes: StockColumnTypes,
            values: new object[] { "WIDGET-01", 10, 0 },
            schema: "orderflow_inventory");
        migrationBuilder.InsertData(
            table: "stock_items",
            columns: StockColumns,
            columnTypes: StockColumnTypes,
            values: new object[] { "WIDGET-02", 5, 0 },
            schema: "orderflow_inventory");
        migrationBuilder.CreateIndex(name: "IX_reservations_OrderId_Sku", schema: "orderflow_inventory", table: "reservations", columns: ReservationIndexColumns, unique: true);
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_EventId", schema: "orderflow_inventory", table: "outbox_messages", column: "EventId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_PublishedAt_LockExpiresAt", schema: "orderflow_inventory", table: "outbox_messages", columns: PublishedLockIndexColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inbox_messages", schema: "orderflow_inventory");
        migrationBuilder.DropTable(name: "reservations", schema: "orderflow_inventory");
        migrationBuilder.DropTable(name: "outbox_messages", schema: "orderflow_inventory");
        migrationBuilder.DropTable(name: "stock_items", schema: "orderflow_inventory");
    }
}
