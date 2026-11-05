namespace SmartPalm.FieldService.Domain.Model.Commands;

public record PlanVisitCommand(long PlantationId, long AgronomistId, DateOnly ScheduledDate, string Objectives);
