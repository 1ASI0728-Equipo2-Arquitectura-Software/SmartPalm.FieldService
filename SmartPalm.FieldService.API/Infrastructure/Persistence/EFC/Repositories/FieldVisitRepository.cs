using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Domain.Repositories;

namespace SmartPalm.FieldService.Infrastructure.Persistence;

internal class FieldVisitRepository(FieldDbContext db) : IFieldVisitRepository
{
    public Task<FieldVisit?> FindAsync(long id, CancellationToken t) => db.Visits.SingleOrDefaultAsync(x => x.Id == id, t);
    public async Task<IReadOnlyList<FieldVisit>> ListAsync(long user, long? plantation, VisitStatus? status, DateOnly? from, DateOnly? to, CancellationToken t) { var q = db.Visits.AsNoTracking().Where(x => x.AgronomistId == user); if (plantation is not null) q = q.Where(x => x.PlantationId == plantation); if (status is not null) q = q.Where(x => x.Status == status); if (from is not null) q = q.Where(x => x.ScheduledDate >= from); if (to is not null) q = q.Where(x => x.ScheduledDate <= to); return await q.OrderBy(x => x.ScheduledDate).ToArrayAsync(t); }
    public void Add(FieldVisit x) => db.Visits.Add(x);
}
