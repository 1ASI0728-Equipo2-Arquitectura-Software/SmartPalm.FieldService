namespace SmartPalm.FieldService.Domain.Model.Commands;

public record LinkInspectionToAlertCommand(long InspectionId, long AlertId);
