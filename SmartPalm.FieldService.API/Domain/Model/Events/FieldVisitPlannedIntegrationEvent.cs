namespace SmartPalm.FieldService.Domain.Model.Events;

public record FieldVisitPlannedIntegrationEvent(long VisitId, long PlantationId, long AgronomistId, DateOnly ScheduledDate, DateTime OccurredAtUtc);
