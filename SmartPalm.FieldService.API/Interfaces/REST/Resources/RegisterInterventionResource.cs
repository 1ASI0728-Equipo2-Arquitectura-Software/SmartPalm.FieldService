namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record RegisterInterventionResource(string description, string performedBy, DateTime executionDate, long? originRecommendationId);
