using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderFlow.Payments.Infrastructure.Persistence;

#nullable disable

namespace OrderFlow.Payments.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PaymentsDbContext))]
[Migration("202609050001_InitialCreate")]
public partial class InitialCreate : Migration
{
    private static readonly string[] PublishedLockIndexColumns = ["PublishedAt", "LockExpiresAt"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "orderflow_payments");

        migrationBuilder.CreateTable(
            name: "payments",
            schema: "orderflow_payments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_payments", x => x.Id));

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: "orderflow_payments",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_inbox_messages", x => x.EventId));

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "orderflow_payments",
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

        migrationBuilder.CreateIndex(name: "IX_payments_OrderId", schema: "orderflow_payments", table: "payments", column: "OrderId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_EventId", schema: "orderflow_payments", table: "outbox_messages", column: "EventId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_PublishedAt_LockExpiresAt", schema: "orderflow_payments", table: "outbox_messages", columns: PublishedLockIndexColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inbox_messages", schema: "orderflow_payments");
        migrationBuilder.DropTable(name: "outbox_messages", schema: "orderflow_payments");
        migrationBuilder.DropTable(name: "payments", schema: "orderflow_payments");
    }
}
