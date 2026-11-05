using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Model.Entities;

public class FieldObservation
{
    private FieldObservation() { Description = PhotoReferencesJson = "[]"; }
    public FieldObservation(string description, ObservationCategory category, ObservationSeverity severity, IEnumerable<string>? photos, DateTime observedAtUtc)
    { if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Observation description is required."); Description = description.Trim(); Category = category; Severity = severity; PhotoReferencesJson = System.Text.Json.JsonSerializer.Serialize(photos?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? []); ObservedAtUtc = observedAtUtc.ToUniversalTime(); }
    public long Id { get; private set; } public long InspectionId { get; private set; } public string Description { get; private set; }
    public ObservationCategory Category { get; private set; } public ObservationSeverity Severity { get; private set; } public string PhotoReferencesJson { get; private set; } public DateTime ObservedAtUtc { get; private set; }
}
