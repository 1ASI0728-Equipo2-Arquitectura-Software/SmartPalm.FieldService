namespace SmartPalm.FieldService.Domain.Model.Events;

public record AlertTriggeredIntegrationEvent(long AlertId, long UserId, long? PlantationId, long? SectorId, string GatewayMac, string DeviceMac, string SensorType, double Value, double Min, double Max, string Level, string Message, DateTime OccurredAtUtc);
