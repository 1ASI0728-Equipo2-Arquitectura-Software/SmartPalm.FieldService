using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Model.Commands;

public record ObservationInput(string Description, ObservationCategory Category, ObservationSeverity Severity, IReadOnlyList<string>? PhotoReferences, DateTime ObservedAtUtc);
