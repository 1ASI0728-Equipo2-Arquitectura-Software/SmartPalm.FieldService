using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Queries;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;

namespace SmartPalm.FieldService.Application.QueryServices;

public class InterventionQueryService(IInterventionRepository repository) : IInterventionQueryService
{
    public Task<AgronomicIntervention?> HandleAsync(GetInterventionByIdQuery q, CancellationToken t) => repository.FindAsync(q.InterventionId, t);
    public Task<IReadOnlyList<AgronomicIntervention>> HandleAsync(GetInterventionsBySectorQuery q, CancellationToken t) => repository.ListBySectorAsync(q.SectorId, t);
    public Task<IReadOnlyList<AgronomicIntervention>> HandleAsync(GetInterventionsByRecommendationQuery q, CancellationToken t) => repository.ListByRecommendationAsync(q.RecommendationId, t);
    public Task<IReadOnlyList<AgronomicIntervention>> HandleAsync(GetInterventionsByPlantationQuery q, CancellationToken t) => repository.ListByPlantationAsync(q.PlantationId, q.SectorId, q.Type, q.Status, q.From, q.To, t);
}
