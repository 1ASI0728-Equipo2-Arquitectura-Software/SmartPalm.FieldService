namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record FieldVisitResource(long id, long plantationId, long agronomistId, DateOnly scheduledDate, DateOnly? actualDate, string status, string objectives, string? cancellationReason, DateTime createdAt, DateTime? completedAt);
