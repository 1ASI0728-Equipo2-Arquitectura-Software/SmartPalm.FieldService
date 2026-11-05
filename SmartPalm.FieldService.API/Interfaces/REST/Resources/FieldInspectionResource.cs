namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record FieldInspectionResource(long id, Guid clientReference, long visitId, long sectorId, long plantationId, long agronomistId, DateTime inspectedAt, DateTime registeredAt, string syncStatus, IReadOnlyList<FieldObservationResource> observations, IReadOnlyList<long> linkedAlertIds);
