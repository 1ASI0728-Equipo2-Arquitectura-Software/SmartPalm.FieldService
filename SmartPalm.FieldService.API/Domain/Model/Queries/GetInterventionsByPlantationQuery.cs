using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Model.Queries;

public record GetInterventionsByPlantationQuery(long PlantationId, long? SectorId, InterventionType? Type, InterventionStatus? Status, DateTime? From, DateTime? To);
