using Microsoft.EntityFrameworkCore;
using OrderFlow.Payments.Infrastructure.Persistence;

namespace OrderFlow.Payments.Infrastructure.Database;

public static class DatabaseInitializer
{
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await db.Database.MigrateAsync();
    }
}
