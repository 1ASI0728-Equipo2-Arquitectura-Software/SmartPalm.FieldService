using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Queries;

namespace SmartPalm.FieldService.Domain.Services;

public interface IFieldVisitQueryService { Task<IReadOnlyList<FieldVisit>> HandleAsync(GetVisitsByAgronomistQuery query, CancellationToken token); Task<FieldVisit?> HandleAsync(GetVisitByIdQuery query, CancellationToken token); }
