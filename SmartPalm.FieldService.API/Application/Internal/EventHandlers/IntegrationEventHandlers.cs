using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Domain.Model.Events;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Infrastructure.Messaging;
using SmartPalm.FieldService.Infrastructure.Persistence;
namespace SmartPalm.FieldService.Application.EventHandlers;
public class IntegrationEventHandlers(IFieldProjectionRepository projections, IInbox inbox, IUnitOfWork unitOfWork,
    FieldDbContext db, IIntegrationEventWriter events)
{
    public async Task HandleAsync(UserDeletionRequestedIntegrationEvent value, CancellationToken token)
    {
        const string eventType = "smartpalm.identity.user-deletion-requested.v1";
        if (await db.InboxMessages.AnyAsync(x => x.Id == value.OperationId, token)) return;

        var plantationIds = await db.Plantations
            .Where(x => x.PalmGrowerId == value.UserId)
            .Select(x => x.PlantationId)
            .ToArrayAsync(token);
        var sectorIds = await db.Sectors
            .Where(x => plantationIds.Contains(x.PlantationId))
            .Select(x => x.SectorId)
            .ToArrayAsync(token);
        var visitIds = await db.Visits
            .Where(x => x.AgronomistId == value.UserId || plantationIds.Contains(x.PlantationId))
            .Select(x => x.Id)
            .ToArrayAsync(token);
        var inspectionIds = await db.Inspections
            .Where(x => x.AgronomistId == value.UserId
                || plantationIds.Contains(x.PlantationId)
                || sectorIds.Contains(x.SectorId)
                || visitIds.Contains(x.VisitId))
            .Select(x => x.Id)
            .ToArrayAsync(token);
        var recommendationIds = await db.Recommendations
            .Where(x => x.AgronomistId == value.UserId || sectorIds.Contains(x.SectorId ?? 0))
            .Select(x => x.RecommendationId)
            .ToArrayAsync(token);
        var alertIds = await db.Alerts
            .Where(x => plantationIds.Contains(x.PlantationId ?? 0) || sectorIds.Contains(x.SectorId ?? 0))
            .Select(x => x.AlertId)
            .ToArrayAsync(token);

        var deleted = 0;
        deleted += await db.Interventions
            .Where(x => x.RegisteredBy == value.UserId
                || x.VerifiedBy == value.UserId
                || plantationIds.Contains(x.PlantationId)
                || sectorIds.Contains(x.SectorId ?? 0)
                || inspectionIds.Contains(x.OriginInspectionId ?? 0)
                || recommendationIds.Contains(x.OriginRecommendationId ?? 0))
            .ExecuteDeleteAsync(token);
        deleted += await db.Inspections
            .Where(x => inspectionIds.Contains(x.Id))
            .ExecuteDeleteAsync(token);
        deleted += await db.Visits
            .Where(x => visitIds.Contains(x.Id))
            .ExecuteDeleteAsync(token);
        deleted += await db.Recommendations
            .Where(x => recommendationIds.Contains(x.RecommendationId))
            .ExecuteDeleteAsync(token);
        deleted += await db.Alerts
            .Where(x => alertIds.Contains(x.AlertId))
            .ExecuteDeleteAsync(token);
        deleted += await db.Affiliations
            .Where(x => x.AgronomistId == value.UserId || plantationIds.Contains(x.PlantationId))
            .ExecuteDeleteAsync(token);
        deleted += await db.Sectors
            .Where(x => plantationIds.Contains(x.PlantationId))
            .ExecuteDeleteAsync(token);
        deleted += await db.Plantations
            .Where(x => x.PalmGrowerId == value.UserId)
            .ExecuteDeleteAsync(token);
        deleted += await db.Users
            .Where(x => x.UserId == value.UserId)
            .ExecuteDeleteAsync(token);

        db.InboxMessages.Add(new InboxMessage
        {
            Id = value.OperationId,
            EventType = eventType,
            ProcessedOnUtc = DateTime.UtcNow
        });
        events.Enqueue("smartpalm.identity.user-deletion-service-completed.v1",
            new UserDeletionServiceCompletedIntegrationEvent(
                value.OperationId, value.UserId, "field", deleted, DateTime.UtcNow));
        await db.SaveChangesAsync(token);
    }

    public Task HandleAsync(Guid id, UserCreatedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "smartpalm.identity.user-created.v1", async ct => { var x = await projections.GetOrCreateUserAsync(e.UserId, ct); x.Role = e.Role; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, SubscriptionCreatedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "smartpalm.identity.subscription-created.v1", async ct => { var x = await projections.GetOrCreateUserAsync(e.UserId, ct); x.HasActiveSubscription = string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase); x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, SubscriptionActivatedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "smartpalm.identity.subscription-activated.v1", async ct => { var x = await projections.GetOrCreateUserAsync(e.UserId, ct); x.HasActiveSubscription = true; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, SubscriptionCancelledIntegrationEvent e, CancellationToken t) => OnceAsync(id, "smartpalm.identity.subscription-cancelled.v1", async ct => { var x = await projections.GetOrCreateUserAsync(e.UserId, ct); x.HasActiveSubscription = false; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, PlantationCreatedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "crop.plantation-created.v1", async ct => { var x = await projections.GetOrCreatePlantationAsync(e.PlantationId, ct); x.PalmGrowerId = e.PalmGrowerId; x.Name = e.Name; x.Active = true; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, SectorAssignedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "crop.sector-assigned.v1", async ct => { var x = await projections.GetOrCreateSectorAsync(e.SectorId, ct); x.PlantationId = e.PlantationId; x.Name = e.Name; x.Active = true; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, SectorRemovedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "crop.sector-removed.v1", async ct => { var x = await projections.GetOrCreateSectorAsync(e.SectorId, ct); x.PlantationId = e.PlantationId; x.Active = false; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, AgronomistAffiliatedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "crop.agronomist-affiliated.v1", async ct => { var x = await projections.GetOrCreateAffiliationAsync(e.AffiliationId, e.AgronomistId, e.PlantationId, ct); x.Active = true; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, AgronomistDetachedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "crop.agronomist-detached.v1", async ct => { foreach (var x in await projections.ListAffiliationsAsync(e.AgronomistId, e.PlantationId, ct)) { x.Active = false; x.UpdatedAtUtc = e.OccurredAtUtc; } }, t);
    public Task HandleAsync(Guid id, AlertTriggeredIntegrationEvent e, CancellationToken t) => OnceAsync(id, "alert.triggered.v1", async ct => { var x = await projections.GetOrCreateAlertAsync(e.AlertId, ct); x.PlantationId = e.PlantationId; x.SectorId = e.SectorId; x.Status = "Active"; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, AlertAcknowledgedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "alert.acknowledged.v1", async ct => { var x = await projections.GetOrCreateAlertAsync(e.AlertId, ct); x.Status = "Acknowledged"; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    public Task HandleAsync(Guid id, RecommendationPublishedIntegrationEvent e, CancellationToken t) => OnceAsync(id, "agronomy.recommendation-published.v1", async ct => { var x = await projections.GetOrCreateRecommendationAsync(e.RecommendationId, ct); x.AgronomistId = e.AgronomistId; x.SectorId = e.SectorId; x.Content = e.Content; x.Published = true; x.UpdatedAtUtc = e.OccurredAtUtc; }, t);
    private Task OnceAsync(Guid id, string type, Func<CancellationToken, Task> handler, CancellationToken token) => unitOfWork.ExecuteAsync(async ct => { if (await inbox.HasProcessedAsync(id, ct)) return false; await handler(ct); inbox.MarkProcessed(id, type); await unitOfWork.SaveChangesAsync(ct); return true; }, token);
}
