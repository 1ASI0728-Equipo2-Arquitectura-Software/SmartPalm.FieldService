using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartPalm.FieldService.Infrastructure.Persistence;
namespace SmartPalm.FieldService.Api;
public class FieldDatabaseHealthCheck(FieldDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default) => await db.Database.CanConnectAsync(token) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Field database is unavailable.");
}
