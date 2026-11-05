using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Entities;

namespace SmartPalm.FieldService.Domain.Services;

public class InterventionTraceabilityService
{
    public void Validate(AgronomicIntervention intervention, RecommendationProjection? recommendation, FieldInspection? inspection)
    {
        if (intervention.OriginRecommendationId is not null && (recommendation is null || !recommendation.Published)) throw new ArgumentException("Origin recommendation does not exist or is not published.");
        if (recommendation?.SectorId is not null && intervention.SectorId is not null && recommendation.SectorId != intervention.SectorId) throw new ArgumentException("Recommendation and intervention sectors do not match.");
        if (inspection is not null && inspection.PlantationId != intervention.PlantationId) throw new ArgumentException("Inspection and intervention plantations do not match.");
    }
}
