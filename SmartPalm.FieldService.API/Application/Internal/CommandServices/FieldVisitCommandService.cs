using SmartPalm.FieldService.Application.DomainServices;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Events;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
namespace SmartPalm.FieldService.Application.CommandServices;
public class FieldVisitCommandService(IFieldVisitRepository visits, IFieldInspectionRepository inspections, IUnitOfWork unitOfWork, IIntegrationEventWriter events, FieldAuthorizationService authorization) : IFieldVisitCommandService
{
    public Task<FieldVisit> HandleAsync(PlanVisitCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct =>
    { await authorization.EnsureAgronomistAccessAsync(c.AgronomistId, c.PlantationId, ct); var value = new FieldVisit(c.PlantationId, c.AgronomistId, c.ScheduledDate, c.Objectives); visits.Add(value); await unitOfWork.SaveChangesAsync(ct); events.Enqueue("field.visit-planned.v1", new FieldVisitPlannedIntegrationEvent(value.Id, value.PlantationId, value.AgronomistId, value.ScheduledDate, DateTime.UtcNow)); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    public Task<FieldVisit> HandleAsync(StartVisitCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct => { var value = await GetAsync(c.VisitId, ct); await authorization.EnsureAgronomistAccessAsync(c.AgronomistId, value.PlantationId, ct); if (value.AgronomistId != c.AgronomistId) throw new UnauthorizedAccessException("Visit belongs to another agronomist."); value.Start(); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    public Task<FieldVisit> HandleAsync(CompleteVisitCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct => { var value = await GetAsync(c.VisitId, ct); await authorization.EnsureAgronomistAccessAsync(c.AgronomistId, value.PlantationId, ct); value.Complete(await inspections.AnyByVisitAsync(value.Id, ct)); events.Enqueue("field.visit-completed.v1", new FieldVisitCompletedIntegrationEvent(value.Id, value.PlantationId, value.AgronomistId, DateTime.UtcNow)); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    public Task<FieldVisit> HandleAsync(CancelVisitCommand c, CancellationToken t) => unitOfWork.ExecuteAsync(async ct => { var value = await GetAsync(c.VisitId, ct); await authorization.EnsureAgronomistAccessAsync(c.AgronomistId, value.PlantationId, ct); value.Cancel(c.Reason); await unitOfWork.SaveChangesAsync(ct); return value; }, t);
    private async Task<FieldVisit> GetAsync(long id, CancellationToken t) => await visits.FindAsync(id, t) ?? throw new KeyNotFoundException("Field visit not found.");
}
