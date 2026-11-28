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
    IReportDraftProvider provider,
    ISoilMoistureReader moisture)
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
                ? row.Description : ClipDescription(row.Description, MaxObservationLength)
        }).ToArray();
        // Sources are already ordered newest first. Use the latest irrigation note per sector, not the request time.
        var irrigation = sources.Where(source => source.IsIrrigation).DistinctBy(source => source.SectorId).ToArray();
        var corroboration = await Task.WhenAll(irrigation.Take(ReportSoilMoisture.MaxSectors)
            .Select(source => ReportSoilMoisture.CompareAsync(moisture, visit.PlantationId, source, token)));
        var draft = await provider.GenerateAsync(sources, corroboration, token);
        ValidateDraft(draft, sources);

        var limitations = new List<string>
        {
            "Borrador generado por IA. Verifique su contenido contra las observaciones antes de usarlo.",
            corroboration.Length == 0 ? "No se consultaron intervenciones, alertas, diagnósticos, sensores ni clima."
                : "Solo se consultó humedad del suelo. Ingestion no verifica si los datos son físicos o simulados."
        };
        if (truncated) limitations.Add("El contexto se limitó a 30 observaciones y 600 caracteres por observación.");
        if (irrigation.Length > ReportSoilMoisture.MaxSectors) limitations.Add("El contraste de humedad se limitó a los tres sectores con observaciones de riego más recientes.");
        if (corroboration.Length == 0)
            return new(new(visit.Id, visit.Status.ToString()), draft.Title,
                draft.Findings, draft.Interpretation, draft.OutstandingChecks, sources, limitations, corroboration);

        // Authoritative findings for irrigated sectors are recorded text. Never present an uncited AI cause or action there.
        var irrigationSectors = irrigation.Select(source => source.SectorId).ToHashSet();
        var findings = sources.Where(source => irrigationSectors.Contains(source.SectorId))
            .Select(source => new ReportDraftFinding(source.SectorId,
                source.Description.Length <= 500 ? source.Description : ClipDescription(source.Description, 500), [source.ObservationId]))
            .Concat(draft.Findings.Where(finding => !irrigationSectors.Contains(finding.SectorId))).Take(30).ToArray();
        return new(new(visit.Id, visit.Status.ToString()), "Borrador técnico: contraste de humedad", findings,
            corroboration.Select(context => context.Assessment).ToArray(), corroboration.Select(context => context.NextCheck).ToArray(),
            sources, limitations, corroboration);
    }

    private static string ClipDescription(string value, int length) =>
        value[..(char.IsHighSurrogate(value[length - 1]) ? length - 1 : length)];

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
