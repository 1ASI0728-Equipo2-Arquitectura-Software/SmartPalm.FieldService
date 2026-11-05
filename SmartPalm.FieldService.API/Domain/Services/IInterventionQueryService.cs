using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Queries;

namespace SmartPalm.FieldService.Domain.Services;

public interface IInterventionQueryService { Task<AgronomicIntervention?> HandleAsync(GetInterventionByIdQuery query, CancellationToken token); Task<IReadOnlyList<AgronomicIntervention>> HandleAsync(GetInterventionsBySectorQuery query, CancellationToken token); Task<IReadOnlyList<AgronomicIntervention>> HandleAsync(GetInterventionsByRecommendationQuery query, CancellationToken token); Task<IReadOnlyList<AgronomicIntervention>> HandleAsync(GetInterventionsByPlantationQuery query, CancellationToken token); }
