using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OrderFlow.Orders.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OrdersDbContext))]
[Migration("202609070001_AddSagaNotification")]
public sealed class AddSagaNotification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION orderflow_orders.notify_saga_log() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_notify('order_saga_log', NEW."OrderId"::text);
            RETURN NEW;
        END; $$ LANGUAGE plpgsql;
        CREATE TRIGGER order_saga_log_notify AFTER INSERT ON orderflow_orders.order_saga_log
        FOR EACH ROW EXECUTE FUNCTION orderflow_orders.notify_saga_log();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER order_saga_log_notify ON orderflow_orders.order_saga_log;
        DROP FUNCTION orderflow_orders.notify_saga_log();
        """);
}
