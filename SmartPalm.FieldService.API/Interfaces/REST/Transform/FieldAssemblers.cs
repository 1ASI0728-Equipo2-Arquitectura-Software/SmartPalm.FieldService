using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Interfaces.REST.Resources;
namespace SmartPalm.FieldService.Interfaces.REST.Transform;
public static class FieldAssemblers
{
    public static FieldVisitResource ToResource(FieldVisit x) => new(x.Id, x.PlantationId, x.AgronomistId, x.ScheduledDate, x.ActualDate, x.Status.ToString(), x.Objectives, x.CancellationReason, x.CreatedAtUtc, x.CompletedAtUtc);
    public static FieldInspectionResource ToResource(FieldInspection x) => new(x.Id, x.ClientReference, x.VisitId, x.SectorId, x.PlantationId, x.AgronomistId, x.InspectedAtUtc, x.RegisteredAtUtc, x.SyncStatus.ToString(), x.Observations.Select(o => new FieldObservationResource(o.Id, o.Description, o.Category.ToString(), o.Severity.ToString(), o.PhotoReferencesJson, o.ObservedAtUtc)).ToArray(), x.AlertLinks.Select(a => a.AlertId).ToArray());
    public static AgronomicInterventionResource ToResource(AgronomicIntervention x) => new(x.Id, x.PlantationId, x.SectorId, x.RegisteredBy, x.PerformedBy, x.Type.ToString(), x.Description, x.ExecutedAtUtc, x.RegisteredAtUtc, x.Status.ToString(), x.OriginRecommendationId, x.OriginInspectionId, x.VerifiedBy, x.VerificationNotes);
    public static LegacyAgronomicInterventionResource ToLegacyResource(AgronomicIntervention x) => new(x.Id, x.SectorId, x.PerformedBy, x.Description, x.ExecutedAtUtc, x.RegisteredAtUtc, x.OriginRecommendationId);
    public static ObservationInput ToInput(ObservationInputResource x) => new(x.description, Parse<ObservationCategory>(x.category), Parse<ObservationSeverity>(x.severity), x.photoReferences, x.observedAt);
    public static VisitStatus? ParseVisitStatus(string? value) => string.IsNullOrWhiteSpace(value) ? null : Parse<VisitStatus>(value);
    public static InterventionType? ParseInterventionType(string? value) => string.IsNullOrWhiteSpace(value) ? null : Parse<InterventionType>(value);
    public static InterventionStatus? ParseInterventionStatus(string? value) => string.IsNullOrWhiteSpace(value) ? null : Parse<InterventionStatus>(value);
    public static T Parse<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) ? parsed : throw new ArgumentException($"Invalid {typeof(T).Name} value '{value}'.");
}
