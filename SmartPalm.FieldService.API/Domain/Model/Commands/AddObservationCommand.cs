namespace SmartPalm.FieldService.Domain.Model.Commands;

public record AddObservationCommand(long InspectionId, ObservationInput Observation);
