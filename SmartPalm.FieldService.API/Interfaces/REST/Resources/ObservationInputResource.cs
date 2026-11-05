namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record ObservationInputResource(string description, string category, string severity, IReadOnlyList<string>? photoReferences, DateTime observedAt);
