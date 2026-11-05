namespace SmartPalm.FieldService.Domain.Model.Queries;

public record GetInspectionsByPlantationQuery(long PlantationId, long? SectorId, DateTime? From, DateTime? To);
