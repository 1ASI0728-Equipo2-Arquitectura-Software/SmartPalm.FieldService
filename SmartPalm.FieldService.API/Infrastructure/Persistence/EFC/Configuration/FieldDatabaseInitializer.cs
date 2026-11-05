using Microsoft.EntityFrameworkCore;
namespace SmartPalm.FieldService.Infrastructure.Persistence;
public static class FieldDatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services) { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<FieldDbContext>().Database.MigrateAsync(); }
}
