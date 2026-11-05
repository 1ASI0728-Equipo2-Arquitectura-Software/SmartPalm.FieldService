namespace SmartPalm.FieldService.Domain.Model.Commands;

public record CompleteVisitCommand(long VisitId, long AgronomistId);
