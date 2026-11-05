using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;

namespace SmartPalm.FieldService.Domain.Services;

public interface IFieldVisitCommandService { Task<FieldVisit> HandleAsync(PlanVisitCommand command, CancellationToken token); Task<FieldVisit> HandleAsync(StartVisitCommand command, CancellationToken token); Task<FieldVisit> HandleAsync(CompleteVisitCommand command, CancellationToken token); Task<FieldVisit> HandleAsync(CancelVisitCommand command, CancellationToken token); }
