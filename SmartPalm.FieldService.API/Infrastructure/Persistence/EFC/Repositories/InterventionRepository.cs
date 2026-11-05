using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Domain.Repositories;

namespace SmartPalm.FieldService.Infrastructure.Persistence;

internal class InterventionRepository(FieldDbContext db) : IInterventionRepository
{
    public Task<AgronomicIntervention?> FindAsync(long id, CancellationToken t) => db.Interventions.SingleOrDefaultAsync(x => x.Id == id, t);
    public async Task<IReadOnlyList<AgronomicIntervention>> ListByPlantationAsync(long plantation, long? sector, InterventionType? type, InterventionStatus? status, DateTime? from, DateTime? to, CancellationToken t) { var q = db.Interventions.AsNoTracking().Where(x => x.PlantationId == plantation); if (sector is not null) q = q.Where(x => x.SectorId == sector); if (type is not null) q = q.Where(x => x.Type == type); if (status is not null) q = q.Where(x => x.Status == status); if (from is not null) q = q.Where(x => x.ExecutedAtUtc >= from); if (to is not null) q = q.Where(x => x.ExecutedAtUtc <= to); return await q.OrderByDescending(x => x.ExecutedAtUtc).ToArrayAsync(t); }
    public async Task<IReadOnlyList<AgronomicIntervention>> ListBySectorAsync(long id, CancellationToken t) => await db.Interventions.AsNoTracking().Where(x => x.SectorId == id).OrderByDescending(x => x.ExecutedAtUtc).ToArrayAsync(t);
    public async Task<IReadOnlyList<AgronomicIntervention>> ListByRecommendationAsync(long id, CancellationToken t) => await db.Interventions.AsNoTracking().Where(x => x.OriginRecommendationId == id).OrderByDescending(x => x.ExecutedAtUtc).ToArrayAsync(t);
    public void Add(AgronomicIntervention x) => db.Interventions.Add(x);
}
