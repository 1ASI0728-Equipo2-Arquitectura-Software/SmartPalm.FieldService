namespace SmartPalm.FieldService.Domain.Model.Events;

public record AgronomistAffiliatedIntegrationEvent(long AffiliationId, long AgronomistId, long PlantationId, DateTime OccurredAtUtc);
