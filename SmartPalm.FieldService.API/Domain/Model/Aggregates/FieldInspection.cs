using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
namespace SmartPalm.FieldService.Domain.Model.Aggregates;
public class FieldInspection
{
    private readonly List<FieldObservation> _observations = []; private readonly List<InspectionAlertLink> _alertLinks = [];
    private FieldInspection() { }
    public FieldInspection(Guid clientReference, long visitId, long sectorId, long plantationId, long agronomistId, DateTime inspectedAtUtc, bool offline)
    {
        if (clientReference == Guid.Empty) throw new ArgumentException("ClientReference is required.");
        if (visitId <= 0 || sectorId <= 0 || plantationId <= 0 || agronomistId <= 0) throw new ArgumentException("Inspection identifiers must be positive.");
        ClientReference = clientReference; VisitId = visitId; SectorId = sectorId; PlantationId = plantationId; AgronomistId = agronomistId;
        InspectedAtUtc = inspectedAtUtc.ToUniversalTime(); RegisteredAtUtc = DateTime.UtcNow; SyncStatus = offline ? SyncStatus.PendingSync : SyncStatus.Synced;
    }
    public long Id { get; private set; } public Guid ClientReference { get; private set; } public long VisitId { get; private set; }
    public long SectorId { get; private set; } public long PlantationId { get; private set; } public long AgronomistId { get; private set; }
    public DateTime InspectedAtUtc { get; private set; } public DateTime RegisteredAtUtc { get; private set; } public SyncStatus SyncStatus { get; private set; }
    public IReadOnlyCollection<FieldObservation> Observations => _observations; public IReadOnlyCollection<InspectionAlertLink> AlertLinks => _alertLinks;
    public void AddObservation(string description, ObservationCategory category, ObservationSeverity severity, IEnumerable<string>? photos, DateTime observedAtUtc) => _observations.Add(new FieldObservation(description, category, severity, photos, observedAtUtc));
    public void LinkAlert(long alertId) { if (alertId <= 0) throw new ArgumentException("AlertId must be positive."); if (_alertLinks.All(x => x.AlertId != alertId)) _alertLinks.Add(new InspectionAlertLink(alertId)); }
    public void MarkSynced() => SyncStatus = SyncStatus.Synced;
}
