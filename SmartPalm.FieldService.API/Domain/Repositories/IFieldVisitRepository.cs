using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Repositories;

public interface IFieldVisitRepository
{
    Task<FieldVisit?> FindAsync(long id, CancellationToken token); Task<IReadOnlyList<FieldVisit>> ListAsync(long agronomistId, long? plantationId, VisitStatus? status, DateOnly? from, DateOnly? to, CancellationToken token); void Add(FieldVisit visit);
}
