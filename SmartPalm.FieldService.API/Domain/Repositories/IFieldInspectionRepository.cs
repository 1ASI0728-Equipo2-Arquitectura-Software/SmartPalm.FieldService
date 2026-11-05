using SmartPalm.FieldService.Domain.Model.Aggregates;

namespace SmartPalm.FieldService.Domain.Repositories;

public interface IFieldInspectionRepository
{
    Task<FieldInspection?> FindAsync(long id, CancellationToken token); Task<FieldInspection?> FindByClientReferenceAsync(Guid clientReference, CancellationToken token); Task<bool> AnyByVisitAsync(long visitId, CancellationToken token);
    Task<IReadOnlyList<FieldInspection>> ListByPlantationAsync(long plantationId, long? sectorId, DateTime? from, DateTime? to, CancellationToken token); void Add(FieldInspection inspection);
}
