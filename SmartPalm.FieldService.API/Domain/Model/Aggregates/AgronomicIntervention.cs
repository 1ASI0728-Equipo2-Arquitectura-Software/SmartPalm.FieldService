using SmartPalm.FieldService.Domain.Model.ValueObjects;
namespace SmartPalm.FieldService.Domain.Model.Aggregates;
public class AgronomicIntervention
{
    private AgronomicIntervention() { Description = PerformedBy = string.Empty; }
    public AgronomicIntervention(long plantationId, long? sectorId, long registeredBy, string performedBy, InterventionType type, string description, long? recommendationId, long? inspectionId, DateTime executedAtUtc)
    {
        if (plantationId <= 0 || registeredBy <= 0) throw new ArgumentException("PlantationId and RegisteredBy must be positive.");
        if (string.IsNullOrWhiteSpace(performedBy) || string.IsNullOrWhiteSpace(description)) throw new ArgumentException("PerformedBy and description are required.");
        PlantationId = plantationId; SectorId = sectorId; RegisteredBy = registeredBy; PerformedBy = performedBy.Trim(); Type = type; Description = description.Trim();
        OriginRecommendationId = recommendationId; OriginInspectionId = inspectionId; ExecutedAtUtc = executedAtUtc.ToUniversalTime(); RegisteredAtUtc = DateTime.UtcNow; Status = InterventionStatus.Registered;
    }
    public long Id { get; private set; } public long PlantationId { get; private set; } public long? SectorId { get; private set; } public long RegisteredBy { get; private set; }
    public string PerformedBy { get; private set; } public InterventionType Type { get; private set; } public string Description { get; private set; }
    public long? OriginRecommendationId { get; private set; } public long? OriginInspectionId { get; private set; } public DateTime ExecutedAtUtc { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; } public InterventionStatus Status { get; private set; } public string? VerificationNotes { get; private set; } public long? VerifiedBy { get; private set; }
    public void Verify(long agronomistId, string notes) { EnsurePending(notes); Status = InterventionStatus.Verified; VerifiedBy = agronomistId; VerificationNotes = notes.Trim(); }
    public void Reject(long agronomistId, string notes) { EnsurePending(notes); Status = InterventionStatus.Rejected; VerifiedBy = agronomistId; VerificationNotes = notes.Trim(); }
    private void EnsurePending(string notes) { if (Status != InterventionStatus.Registered) throw new InvalidOperationException("Only registered interventions can be reviewed."); if (string.IsNullOrWhiteSpace(notes)) throw new ArgumentException("Verification notes are required."); }
}
