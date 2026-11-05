namespace SmartPalm.FieldService.Domain.Model.Commands;

public record RejectInterventionCommand(long InterventionId, long AgronomistId, string Notes);
