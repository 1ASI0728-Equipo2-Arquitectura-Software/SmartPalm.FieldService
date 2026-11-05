using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Repositories;

namespace SmartPalm.FieldService.Infrastructure.Persistence;

internal class FieldInspectionRepository(FieldDbContext db) : IFieldInspectionRepository
{
    private IQueryable<FieldInspection> Full => db.Inspections.Include(x => x.Observations).Include(x => x.AlertLinks);
    public Task<FieldInspection?> FindAsync(long id, CancellationToken t) => Full.SingleOrDefaultAsync(x => x.Id == id, t); public Task<FieldInspection?> FindByClientReferenceAsync(Guid id, CancellationToken t) => Full.SingleOrDefaultAsync(x => x.ClientReference == id, t); public Task<bool> AnyByVisitAsync(long id, CancellationToken t) => db.Inspections.AnyAsync(x => x.VisitId == id, t);
    public async Task<IReadOnlyList<FieldInspection>> ListByPlantationAsync(long plantation, long? sector, DateTime? from, DateTime? to, CancellationToken t) { var q = Full.AsNoTracking().Where(x => x.PlantationId == plantation); if (sector is not null) q = q.Where(x => x.SectorId == sector); if (from is not null) q = q.Where(x => x.InspectedAtUtc >= from); if (to is not null) q = q.Where(x => x.InspectedAtUtc <= to); return await q.OrderByDescending(x => x.InspectedAtUtc).ToArrayAsync(t); }
    public void Add(FieldInspection x) => db.Inspections.Add(x);
}
