namespace SmartPalm.FieldService.Interfaces.REST.Resources;

public record AgronomicInterventionResource(long id, long plantationId, long? sectorId, long registeredBy, string performedBy, string type, string description, DateTime executionDate, DateTime createdAt, string status, long? recommendationId, long? inspectionId, long? verifiedBy, string? verificationNotes);
