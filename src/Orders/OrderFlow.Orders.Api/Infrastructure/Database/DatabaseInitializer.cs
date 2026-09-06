using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Infrastructure.Persistence;

namespace OrderFlow.Orders.Infrastructure.Database;

public static class DatabaseInitializer
{
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await db.Database.MigrateAsync();
    }
}
