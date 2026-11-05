using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;

namespace SmartPalm.FieldService.Domain.Services;

public interface IFieldInspectionCommandService { Task<FieldInspection> HandleAsync(RegisterInspectionCommand command, CancellationToken token); Task<IReadOnlyList<FieldInspection>> HandleAsync(SyncOfflineInspectionsCommand command, CancellationToken token); Task<FieldInspection> HandleAsync(AddObservationCommand command, CancellationToken token); Task<FieldInspection> HandleAsync(LinkInspectionToAlertCommand command, CancellationToken token); }
