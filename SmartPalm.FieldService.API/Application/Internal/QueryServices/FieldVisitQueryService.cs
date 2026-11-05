using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Queries;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;

namespace SmartPalm.FieldService.Application.QueryServices;

public class FieldVisitQueryService(IFieldVisitRepository repository) : IFieldVisitQueryService
{
    public Task<IReadOnlyList<FieldVisit>> HandleAsync(GetVisitsByAgronomistQuery q, CancellationToken t) => repository.ListAsync(q.AgronomistId, q.PlantationId, q.Status, q.From, q.To, t);
    public Task<FieldVisit?> HandleAsync(GetVisitByIdQuery q, CancellationToken t) => repository.FindAsync(q.VisitId, t);
}
