namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record FieldObservationResource(long id, string description, string category, string severity, string photoReferencesJson, DateTime observedAt);
