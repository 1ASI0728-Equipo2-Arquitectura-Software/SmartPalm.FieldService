using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Queries;

namespace SmartPalm.FieldService.Domain.Services;

public interface IFieldInspectionQueryService { Task<FieldInspection?> HandleAsync(GetInspectionByIdQuery query, CancellationToken token); Task<IReadOnlyList<FieldInspection>> HandleAsync(GetInspectionsByPlantationQuery query, CancellationToken token); }
