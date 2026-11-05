using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Model.Commands;

public record RegisterInterventionCommand(long PlantationId, long? SectorId, long RegisteredBy, string PerformedBy, InterventionType Type, string Description, long? OriginRecommendationId, long? OriginInspectionId, DateTime ExecutedAtUtc);
