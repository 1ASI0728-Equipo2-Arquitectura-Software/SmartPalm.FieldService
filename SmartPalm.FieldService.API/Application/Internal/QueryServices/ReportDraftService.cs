using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Domain.Repositories;

namespace SmartPalm.FieldService.Application.QueryServices;

public enum ReportDraftFailure { NotFound, Forbidden, VisitNotCompleted, InsufficientEvidence, ProviderUnavailable, ProviderTimeout }

public sealed class ReportDraftException(ReportDraftFailure failure) : Exception(failure.ToString())
{
    public ReportDraftFailure Failure { get; } = failure;
}

public sealed class ReportDraftService(
    IFieldVisitRepository visits,
    IFieldProjectionRepository projections,
    IReportDraftEvidenceRepository evidence,
    IReportDraftProvider provider)
{
    private const int MaxObservations = 30;
    private const int MaxObservationLength = 600;

    public async Task<ReportDraftResult> GenerateAsync(long actorId, long visitId, CancellationToken token)
    {
        var visit = await visits.FindAsync(visitId, token)
            ?? throw new ReportDraftException(ReportDraftFailure.NotFound);
        if (visit.AgronomistId != actorId
            || !string.Equals((await projections.FindUserAsync(actorId, token))?.Role, "Agronomist", StringComparison.OrdinalIgnoreCase)
            || (await projections.FindPlantationAsync(visit.PlantationId, token))?.Active != true
            || !await projections.HasAffiliationAsync(actorId, visit.PlantationId, token))
            throw new ReportDraftException(ReportDraftFailure.Forbidden);
        if (visit.Status != VisitStatus.Completed)
            throw new ReportDraftException(ReportDraftFailure.VisitNotCompleted);

        var rows = await evidence.ListAsync(visitId, visit.PlantationId, MaxObservations + 1, MaxObservationLength + 1, token);
        if (rows.Count == 0)
            throw new ReportDraftException(ReportDraftFailure.InsufficientEvidence);

        var truncated = rows.Count > MaxObservations || rows.Take(MaxObservations).Any(row => row.Description.Length > MaxObservationLength);
        var sources = rows.Take(MaxObservations).Select(row => row with
        {
            Description = row.Description.Length <= MaxObservationLength
                ? row.Description : ClipDescription(row.Description)
        }).ToArray();
        var draft = await provider.GenerateAsync(sources, token);
        ValidateDraft(draft, sources);

        var limitations = new List<string>
        {
            "Borrador generado por IA. Verifique su contenido contra las observaciones antes de usarlo.",
            "No se consultaron intervenciones, alertas, diagnósticos, sensores ni clima."
        };
        if (truncated) limitations.Add("El contexto se limitó a 30 observaciones y 600 caracteres por observación.");
        return new ReportDraftResult(new(visit.Id, visit.Status.ToString()), draft.Title,
            draft.Findings, draft.Interpretation, draft.OutstandingChecks, sources, limitations);
    }

    private static string ClipDescription(string value) =>
        value[..(char.IsHighSurrogate(value[MaxObservationLength - 1]) ? MaxObservationLength - 1 : MaxObservationLength)];

    private static void ValidateDraft(ReportDraftContent draft, IReadOnlyList<ReportDraftSource> sources)
    {
        var sourceSectors = sources.ToDictionary(source => source.ObservationId, source => source.SectorId);
        if (!ValidText(draft.Title, 200) || draft.Findings is not { Length: > 0 and <= 30 }
            || draft.Findings.Any(finding => finding is null || !ValidText(finding.Text, 500)
                || finding.ObservationIds is not { Length: > 0 and <= MaxObservations }
                || finding.ObservationIds.Distinct().Count() != finding.ObservationIds.Length
                || finding.ObservationIds.Any(id => !sourceSectors.TryGetValue(id, out var sector) || sector != finding.SectorId))
            || !ValidTextList(draft.Interpretation) || !ValidTextList(draft.OutstandingChecks))
            throw new ReportDraftException(ReportDraftFailure.ProviderUnavailable);
    }

    private static bool ValidText(string? text, int length) => !string.IsNullOrWhiteSpace(text) && text.Length <= length;
    private static bool ValidTextList(string[]? values) => values is { Length: <= 12 } && values.All(value => ValidText(value, 500));
}
