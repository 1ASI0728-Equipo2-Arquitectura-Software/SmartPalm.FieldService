using SmartPalm.FieldService.Application.DomainServices;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Events;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
namespace SmartPalm.FieldService.Application.CommandServices;
public class FieldInspectionCommandService(IFieldVisitRepository visits, IFieldInspectionRepository inspections, IFieldProjectionRepository projections, IUnitOfWork unitOfWork, IIntegrationEventWriter events, FieldAuthorizationService authorization) : IFieldInspectionCommandService
{
    public Task<FieldInspection> HandleAsync(RegisterInspectionCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(ct => RegisterCoreAsync(c, ct), t);
    public Task<IReadOnlyList<FieldInspection>> HandleAsync(SyncOfflineInspectionsCommand c, CancellationToken t) => unitOfWork.ExecuteAsync<IReadOnlyList<FieldInspection>>(async ct =>
    { var result = new List<FieldInspection>(); foreach (var item in c.Inspections) { var existing = await inspections.FindByClientReferenceAsync(item.ClientReference, ct); result.Add(existing ?? await RegisterCoreAsync(item with { VisitId = c.VisitId, AgronomistId = c.AgronomistId, Offline = true }, ct)); } return result; }, t);
    public Task<FieldInspection> HandleAsync(AddObservationCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct => { var value = await GetAsync(c.InspectionId, ct); var o = c.Observation; value.AddObservation(o.Description, o.Category, o.Severity, o.PhotoReferences, o.ObservedAtUtc); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    public Task<FieldInspection> HandleAsync(LinkInspectionToAlertCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct => { var value = await GetAsync(c.InspectionId, ct); var alert = await projections.FindAlertAsync(c.AlertId, ct); if (alert is null || !string.Equals(alert.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Alert does not exist or is not active."); if (alert.SectorId is not null && alert.SectorId != value.SectorId) throw new ArgumentException("Alert belongs to a different sector."); value.LinkAlert(c.AlertId); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    private async Task<FieldInspection> RegisterCoreAsync(RegisterInspectionCommand c, CancellationToken ct)
    {
        var existing = await inspections.FindByClientReferenceAsync(c.ClientReference, ct); if (existing is not null) return existing;
        var visit = await visits.FindAsync(c.VisitId, ct) ?? throw new ArgumentException("Field visit does not exist."); if (visit.Status != VisitStatus.InProgress) throw new InvalidOperationException("Inspections require a visit in progress."); if (visit.AgronomistId != c.AgronomistId) throw new UnauthorizedAccessException("Visit belongs to another agronomist.");
        await authorization.EnsureAgronomistAccessAsync(c.AgronomistId, visit.PlantationId, ct); var sector = await projections.FindSectorAsync(c.SectorId, ct); if (sector is null || !sector.Active || sector.PlantationId != visit.PlantationId) throw new ArgumentException("Sector does not belong to the visit plantation.");
        var value = new FieldInspection(c.ClientReference, c.VisitId, c.SectorId, visit.PlantationId, c.AgronomistId, c.InspectedAtUtc, c.Offline);
        foreach (var o in c.Observations) value.AddObservation(o.Description, o.Category, o.Severity, o.PhotoReferences, o.ObservedAtUtc);
        foreach (var alertId in c.LinkedAlertIds ?? []) { var alert = await projections.FindAlertAsync(alertId, ct); if (alert is null || !string.Equals(alert.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Alert {alertId} is not active."); value.LinkAlert(alertId); }
        inspections.Add(value); await unitOfWork.SaveChangesAsync(ct); if (c.Offline) value.MarkSynced();
        events.Enqueue("field.inspection-registered.v1", new FieldInspectionRegisteredIntegrationEvent(value.Id, value.ClientReference, value.VisitId, value.PlantationId, value.SectorId, value.AgronomistId, value.InspectedAtUtc, DateTime.UtcNow)); await unitOfWork.SaveChangesAsync(ct); return value;
    }
    private async Task<FieldInspection> GetAsync(long id, CancellationToken t) => await inspections.FindAsync(id, t) ?? throw new KeyNotFoundException("Field inspection not found.");
}
