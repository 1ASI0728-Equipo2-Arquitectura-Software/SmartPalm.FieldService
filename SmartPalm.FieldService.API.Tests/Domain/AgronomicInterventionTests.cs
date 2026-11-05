using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Domain.Services;
namespace SmartPalm.FieldService.API.Tests.Domain;
public class AgronomicInterventionTests
{
    [Fact] public void Verify_RecordsReviewerAndNotes() { var x = new AgronomicIntervention(1, 2, 3, "Field team", InterventionType.Fertilization, "Apply fertilizer", null, null, DateTime.UtcNow); x.Verify(4, "Evidence reviewed"); Assert.Equal(InterventionStatus.Verified, x.Status); Assert.Equal(4, x.VerifiedBy); }
    [Fact] public void Review_CannotRunTwice() { var x = new AgronomicIntervention(1, null, 3, "Field team", InterventionType.Other, "Maintenance", null, null, DateTime.UtcNow); x.Reject(4, "Insufficient evidence"); Assert.Throws<InvalidOperationException>(() => x.Verify(4, "Retry")); }
    [Fact] public void Traceability_RequiresPublishedRecommendationProjection()
    {
        var intervention = new AgronomicIntervention(1, 2, 3, "Field team", InterventionType.Fertilization, "Apply fertilizer", 88, null, DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => new InterventionTraceabilityService().Validate(intervention, null, null));
    }
    [Fact] public void Traceability_AcceptsPublishedRecommendationProjection()
    {
        var intervention = new AgronomicIntervention(1, 2, 3, "Field team", InterventionType.Fertilization, "Apply fertilizer", 88, null, DateTime.UtcNow);
        var recommendation = new RecommendationProjection { RecommendationId = 88, SectorId = 2, Published = true };
        new InterventionTraceabilityService().Validate(intervention, recommendation, null);
    }
}
