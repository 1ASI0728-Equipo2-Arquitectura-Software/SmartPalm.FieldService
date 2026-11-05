namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record RegisterInspectionResource(Guid clientReference, long sectorId, long agronomistId, IReadOnlyList<ObservationInputResource> observations, DateTime inspectedAt, IReadOnlyList<long>? linkedAlertIds, bool offline = false);
