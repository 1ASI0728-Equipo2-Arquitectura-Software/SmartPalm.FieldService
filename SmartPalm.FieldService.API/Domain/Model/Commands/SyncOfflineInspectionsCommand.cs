namespace SmartPalm.FieldService.Domain.Model.Commands;

public record SyncOfflineInspectionsCommand(long VisitId, long AgronomistId, IReadOnlyList<RegisterInspectionCommand> Inspections);
