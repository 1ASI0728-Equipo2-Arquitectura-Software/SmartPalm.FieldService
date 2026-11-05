namespace SmartPalm.FieldService.Domain.Model.Events;

public record FieldVisitCompletedIntegrationEvent(long VisitId, long PlantationId, long AgronomistId, DateTime OccurredAtUtc);
