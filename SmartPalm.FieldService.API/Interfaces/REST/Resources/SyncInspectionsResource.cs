namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record SyncInspectionsResource(long agronomistId, IReadOnlyList<SyncInspectionItemResource> inspections);
