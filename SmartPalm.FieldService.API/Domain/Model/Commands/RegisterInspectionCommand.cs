namespace SmartPalm.FieldService.Domain.Model.Commands;

public record RegisterInspectionCommand(Guid ClientReference, long VisitId, long SectorId, long AgronomistId, IReadOnlyList<ObservationInput> Observations, DateTime InspectedAtUtc, IReadOnlyList<long>? LinkedAlertIds, bool Offline);
