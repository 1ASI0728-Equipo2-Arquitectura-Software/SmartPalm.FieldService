namespace SmartPalm.FieldService.Domain.Model.Events;

public record SectorAssignedIntegrationEvent(long SectorId, long PlantationId, string DeviceMacAddress, string Name, DateTime OccurredAtUtc);
