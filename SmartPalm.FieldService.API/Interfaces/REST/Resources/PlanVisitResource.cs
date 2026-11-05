namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record PlanVisitResource(long plantationId, long agronomistId, DateOnly scheduledDate, string objectives);
