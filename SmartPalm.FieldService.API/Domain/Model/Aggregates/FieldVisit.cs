using SmartPalm.FieldService.Domain.Model.ValueObjects;
namespace SmartPalm.FieldService.Domain.Model.Aggregates;
public class FieldVisit
{
    private FieldVisit() { Objectives = string.Empty; }
    public FieldVisit(long plantationId, long agronomistId, DateOnly scheduledDate, string objectives)
    {
        if (plantationId <= 0 || agronomistId <= 0) throw new ArgumentException("PlantationId and AgronomistId must be positive.");
        if (scheduledDate < DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("Scheduled date cannot be in the past.");
        if (string.IsNullOrWhiteSpace(objectives)) throw new ArgumentException("Visit objectives cannot be empty.");
        PlantationId = plantationId; AgronomistId = agronomistId; ScheduledDate = scheduledDate; Objectives = objectives.Trim(); Status = VisitStatus.Planned; CreatedAtUtc = DateTime.UtcNow;
    }
    public long Id { get; private set; } public long PlantationId { get; private set; } public long AgronomistId { get; private set; }
    public DateOnly ScheduledDate { get; private set; } public DateOnly? ActualDate { get; private set; } public VisitStatus Status { get; private set; }
    public string Objectives { get; private set; } public string? CancellationReason { get; private set; } public DateTime CreatedAtUtc { get; private set; } public DateTime? CompletedAtUtc { get; private set; }
    public void Start() { if (Status != VisitStatus.Planned) throw new InvalidOperationException("Only planned visits can be started."); Status = VisitStatus.InProgress; ActualDate = DateOnly.FromDateTime(DateTime.UtcNow); }
    public void Complete(bool hasInspections) { if (Status != VisitStatus.InProgress) throw new InvalidOperationException("Only visits in progress can be completed."); if (!hasInspections) throw new InvalidOperationException("A visit requires at least one inspection before completion."); Status = VisitStatus.Completed; CompletedAtUtc = DateTime.UtcNow; }
    public void Cancel(string reason) { if (Status != VisitStatus.Planned) throw new InvalidOperationException("Only planned visits can be cancelled."); if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required."); Status = VisitStatus.Cancelled; CancellationReason = reason.Trim(); }
}
