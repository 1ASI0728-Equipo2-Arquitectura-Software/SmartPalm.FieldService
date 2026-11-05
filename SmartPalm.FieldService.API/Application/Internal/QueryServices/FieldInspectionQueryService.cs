using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Queries;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;

namespace SmartPalm.FieldService.Application.QueryServices;

public class FieldInspectionQueryService(IFieldInspectionRepository repository) : IFieldInspectionQueryService
{
    public Task<FieldInspection?> HandleAsync(GetInspectionByIdQuery q, CancellationToken t) => repository.FindAsync(q.InspectionId, t);
    public Task<IReadOnlyList<FieldInspection>> HandleAsync(GetInspectionsByPlantationQuery q, CancellationToken t) => repository.ListByPlantationAsync(q.PlantationId, q.SectorId, q.From, q.To, t);
}
