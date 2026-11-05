using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
namespace SmartPalm.FieldService.API.Tests.Domain;
public class FieldInspectionTests
{
    [Fact] public void AlertLink_IsIdempotent() { var x = new FieldInspection(Guid.NewGuid(), 1, 2, 3, 4, DateTime.UtcNow, true); x.LinkAlert(8); x.LinkAlert(8); Assert.Single(x.AlertLinks); }
    [Fact] public void OfflineInspection_CanBeMarkedSynced() { var x = new FieldInspection(Guid.NewGuid(), 1, 2, 3, 4, DateTime.UtcNow, true); Assert.Equal(SyncStatus.PendingSync, x.SyncStatus); x.MarkSynced(); Assert.Equal(SyncStatus.Synced, x.SyncStatus); }
}
