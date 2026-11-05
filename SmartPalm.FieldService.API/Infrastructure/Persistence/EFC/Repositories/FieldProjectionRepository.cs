using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Repositories;

namespace SmartPalm.FieldService.Infrastructure.Persistence;

internal class FieldProjectionRepository(FieldDbContext db) : IFieldProjectionRepository
{
    public Task<UserProjection?> FindUserAsync(long id, CancellationToken t) => db.Users.SingleOrDefaultAsync(x => x.UserId == id, t);
    public Task<PlantationProjection?> FindPlantationAsync(long id, CancellationToken t) => db.Plantations.SingleOrDefaultAsync(x => x.PlantationId == id, t);
    public Task<SectorProjection?> FindSectorAsync(long id, CancellationToken t) => db.Sectors.SingleOrDefaultAsync(x => x.SectorId == id, t);
    public Task<AlertProjection?> FindAlertAsync(long id, CancellationToken t) => db.Alerts.SingleOrDefaultAsync(x => x.AlertId == id, t);
    public Task<RecommendationProjection?> FindRecommendationAsync(long id, CancellationToken t) => db.Recommendations.SingleOrDefaultAsync(x => x.RecommendationId == id, t);
    public Task<bool> HasAffiliationAsync(long agronomistId, long plantationId, CancellationToken t) => db.Affiliations.AnyAsync(x => x.AgronomistId == agronomistId && x.PlantationId == plantationId && x.Active, t);
    public async Task<UserProjection> GetOrCreateUserAsync(long id, CancellationToken t) => await db.Users.FindAsync([id], t) ?? Add(db.Users, new UserProjection { UserId = id });
    public async Task<PlantationProjection> GetOrCreatePlantationAsync(long id, CancellationToken t) => await db.Plantations.FindAsync([id], t) ?? Add(db.Plantations, new PlantationProjection { PlantationId = id });
    public async Task<SectorProjection> GetOrCreateSectorAsync(long id, CancellationToken t) => await db.Sectors.FindAsync([id], t) ?? Add(db.Sectors, new SectorProjection { SectorId = id });
    public async Task<AgronomistAffiliationProjection> GetOrCreateAffiliationAsync(long id, long agronomistId, long plantationId, CancellationToken t) => await db.Affiliations.FindAsync([id], t) ?? Add(db.Affiliations, new AgronomistAffiliationProjection { AffiliationId = id, AgronomistId = agronomistId, PlantationId = plantationId });
    public async Task<IReadOnlyList<AgronomistAffiliationProjection>> ListAffiliationsAsync(long agronomistId, long plantationId, CancellationToken t) => await db.Affiliations.Where(x => x.AgronomistId == agronomistId && x.PlantationId == plantationId).ToArrayAsync(t);
    public async Task<AlertProjection> GetOrCreateAlertAsync(long id, CancellationToken t) => await db.Alerts.FindAsync([id], t) ?? Add(db.Alerts, new AlertProjection { AlertId = id });
    public async Task<RecommendationProjection> GetOrCreateRecommendationAsync(long id, CancellationToken t) => await db.Recommendations.FindAsync([id], t) ?? Add(db.Recommendations, new RecommendationProjection { RecommendationId = id });
    private static T Add<T>(DbSet<T> set, T value) where T : class { set.Add(value); return value; }
}
