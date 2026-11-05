namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record LegacyAgronomicInterventionResource(long id, long? sectorId, string performedBy, string description, DateTime executionDate, DateTime createdAt, long? recommendationId);
