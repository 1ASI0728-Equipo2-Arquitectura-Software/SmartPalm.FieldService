namespace SmartPalm.FieldService.Domain.Model.Events;

public record AgronomistDetachedIntegrationEvent(long AgronomistId, long PlantationId, DateTime OccurredAtUtc);
