namespace SmartPalm.FieldService.Domain.Model.Events;

public record AgronomicInterventionRegisteredIntegrationEvent(long InterventionId, long PlantationId, long? SectorId, long RegisteredBy, long? RecommendationId, long? InspectionId, string Type, DateTime ExecutedAtUtc, DateTime OccurredAtUtc);
