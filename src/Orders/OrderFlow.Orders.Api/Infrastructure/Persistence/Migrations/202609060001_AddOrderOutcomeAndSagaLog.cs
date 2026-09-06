using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderFlow.Orders.Infrastructure.Persistence;

#nullable disable

namespace OrderFlow.Orders.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OrdersDbContext))]
[Migration("202609060001_AddOrderOutcomeAndSagaLog")]
public partial class AddOrderOutcomeAndSagaLog : Migration
{
    private static readonly string[] SagaLogOrderEventColumns = ["OrderId", "EventType"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CompletedAt", schema: "orderflow_orders", table: "orders",
            type: "timestamp with time zone", nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FailureStage", schema: "orderflow_orders", table: "orders",
            type: "character varying(30)", maxLength: 30, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FailureReason", schema: "orderflow_orders", table: "orders",
            type: "character varying(500)", maxLength: 500, nullable: true);

        migrationBuilder.CreateTable(
            name: "order_saga_log",
            schema: "orderflow_orders",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Seq = table.Column<int>(type: "integer", nullable: false),
                EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Detail = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_order_saga_log", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_order_saga_log_OrderId_EventType", schema: "orderflow_orders",
            table: "order_saga_log", columns: SagaLogOrderEventColumns, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "order_saga_log", schema: "orderflow_orders");
        migrationBuilder.DropColumn(name: "CompletedAt", schema: "orderflow_orders", table: "orders");
        migrationBuilder.DropColumn(name: "FailureStage", schema: "orderflow_orders", table: "orders");
        migrationBuilder.DropColumn(name: "FailureReason", schema: "orderflow_orders", table: "orders");
    }
}
