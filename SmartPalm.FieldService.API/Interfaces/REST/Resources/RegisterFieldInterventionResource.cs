namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record RegisterFieldInterventionResource(long plantationId, long? sectorId, long registeredBy, string performedBy, string type, string description, DateTime executionDate, long? originRecommendationId = null, long? originInspectionId = null);
