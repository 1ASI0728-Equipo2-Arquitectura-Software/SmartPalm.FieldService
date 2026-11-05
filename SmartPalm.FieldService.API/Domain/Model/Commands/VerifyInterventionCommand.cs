namespace SmartPalm.FieldService.Domain.Model.Commands;

public record VerifyInterventionCommand(long InterventionId, long AgronomistId, string Notes);
