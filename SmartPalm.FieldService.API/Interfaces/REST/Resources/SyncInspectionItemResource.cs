namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record SyncInspectionItemResource(Guid clientReference, long sectorId, IReadOnlyList<ObservationInputResource> observations, DateTime inspectedAt, IReadOnlyList<long>? linkedAlertIds);
