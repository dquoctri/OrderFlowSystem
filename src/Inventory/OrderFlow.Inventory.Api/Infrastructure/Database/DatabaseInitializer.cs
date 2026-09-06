using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Infrastructure.Persistence;

namespace OrderFlow.Inventory.Infrastructure.Database;

public static class DatabaseInitializer
{
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.MigrateAsync();
    }
}
