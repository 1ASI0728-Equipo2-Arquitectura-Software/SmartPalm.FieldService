using SmartPalm.FieldService.Application.DomainServices;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Events;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
namespace SmartPalm.FieldService.Application.CommandServices;
public class InterventionCommandService(IInterventionRepository interventions, IFieldInspectionRepository inspections, IFieldProjectionRepository projections, IUnitOfWork unitOfWork, IIntegrationEventWriter events, FieldAuthorizationService authorization, InterventionTraceabilityService traceability) : IInterventionCommandService
{
    public Task<AgronomicIntervention> HandleAsync(RegisterInterventionCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct =>
    { await authorization.EnsureFieldUserAccessAsync(c.RegisteredBy, c.PlantationId, ct); if (c.SectorId is not null) { var sector = await projections.FindSectorAsync(c.SectorId.Value, ct); if (sector is null || !sector.Active || sector.PlantationId != c.PlantationId) throw new ArgumentException("Sector does not belong to the plantation."); }
      var recommendation = c.OriginRecommendationId is null ? null : await projections.FindRecommendationAsync(c.OriginRecommendationId.Value, ct); var inspection = c.OriginInspectionId is null ? null : await inspections.FindAsync(c.OriginInspectionId.Value, ct);
      var value = new AgronomicIntervention(c.PlantationId, c.SectorId, c.RegisteredBy, c.PerformedBy, c.Type, c.Description, c.OriginRecommendationId, c.OriginInspectionId, c.ExecutedAtUtc); traceability.Validate(value, recommendation, inspection);
      interventions.Add(value); await unitOfWork.SaveChangesAsync(ct); events.Enqueue("field.intervention-registered.v1", new AgronomicInterventionRegisteredIntegrationEvent(value.Id, value.PlantationId, value.SectorId, value.RegisteredBy, value.OriginRecommendationId, value.OriginInspectionId, value.Type.ToString(), value.ExecutedAtUtc, DateTime.UtcNow)); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    public Task<AgronomicIntervention> HandleAsync(VerifyInterventionCommand c, CancellationToken t) => ReviewAsync(c.InterventionId, c.AgronomistId, c.Notes, true, t);
    public Task<AgronomicIntervention> HandleAsync(RejectInterventionCommand c, CancellationToken t) => ReviewAsync(c.InterventionId, c.AgronomistId, c.Notes, false, t);
    private Task<AgronomicIntervention> ReviewAsync(long id, long agronomistId, string notes, bool verify, CancellationToken t) => unitOfWork.ExecuteAsync(async ct => { var value = await interventions.FindAsync(id, ct) ?? throw new KeyNotFoundException("Intervention not found."); await authorization.EnsureAgronomistAccessAsync(agronomistId, value.PlantationId, ct); if (verify) value.Verify(agronomistId, notes); else value.Reject(agronomistId, notes); events.Enqueue("field.intervention-reviewed.v1", new AgronomicInterventionReviewedIntegrationEvent(value.Id, value.Status.ToString(), agronomistId, DateTime.UtcNow)); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
}
