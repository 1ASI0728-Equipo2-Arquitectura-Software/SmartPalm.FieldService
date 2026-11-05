namespace SmartPalm.FieldService.Domain.Model.Events;

public record FieldInspectionRegisteredIntegrationEvent(long InspectionId, Guid ClientReference, long VisitId, long PlantationId, long SectorId, long AgronomistId, DateTime InspectedAtUtc, DateTime OccurredAtUtc);
