namespace SmartPalm.FieldService.Domain.Model.Events;

public record AgronomicInterventionReviewedIntegrationEvent(long InterventionId, string Status, long AgronomistId, DateTime OccurredAtUtc);
