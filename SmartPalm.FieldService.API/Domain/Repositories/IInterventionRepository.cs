using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Repositories;

public interface IInterventionRepository
{
    Task<AgronomicIntervention?> FindAsync(long id, CancellationToken token); Task<IReadOnlyList<AgronomicIntervention>> ListByPlantationAsync(long plantationId, long? sectorId, InterventionType? type, InterventionStatus? status, DateTime? from, DateTime? to, CancellationToken token);
    Task<IReadOnlyList<AgronomicIntervention>> ListBySectorAsync(long sectorId, CancellationToken token); Task<IReadOnlyList<AgronomicIntervention>> ListByRecommendationAsync(long recommendationId, CancellationToken token); void Add(AgronomicIntervention intervention);
}
