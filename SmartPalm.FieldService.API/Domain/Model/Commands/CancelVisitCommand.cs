namespace SmartPalm.FieldService.Domain.Model.Commands;

public record CancelVisitCommand(long VisitId, long AgronomistId, string Reason);
