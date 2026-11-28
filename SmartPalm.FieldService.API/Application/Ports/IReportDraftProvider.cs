using System.Text.Json.Serialization;

namespace SmartPalm.FieldService.Application.Ports;

public interface IReportDraftProvider
{
    Task<ReportDraftContent> GenerateAsync(IReadOnlyList<ReportDraftSource> evidence,
        IReadOnlyList<ReportSoilMoistureContext> moistureContext, CancellationToken token);
}

public interface IReportDraftEvidenceRepository
{
    Task<IReadOnlyList<ReportDraftSource>> ListAsync(long visitId, long plantationId, int limit, int descriptionLimit, CancellationToken token);
}

public sealed record ReportDraftSource(long ObservationId, long InspectionId, long SectorId, DateTime ObservedAtUtc,
    string Description, [property: JsonIgnore] bool IsIrrigation = false);
public sealed record ReportDraftFinding(long SectorId, string Text, long[] ObservationIds);
public sealed record ReportDraftContent(string Title, ReportDraftFinding[] Findings, string[] Interpretation, string[] OutstandingChecks);
public sealed record ReportDraftVisit(long Id, string Status);
public sealed record ReportDraftResult(ReportDraftVisit Visit, string Title, ReportDraftFinding[] Findings,
    string[] Interpretation, string[] OutstandingChecks, IReadOnlyList<ReportDraftSource> Sources, IReadOnlyList<string> Limitations,
    IReadOnlyList<ReportSoilMoistureContext> SoilMoistureContext);

public sealed record ReportSoilMoisturePeriod(int SampleCount, decimal MedianPercent,
    DateTimeOffset FirstMeasuredAtUtc, DateTimeOffset LastMeasuredAtUtc);

public sealed record ReportSoilMoistureContext(long SectorId, long ReferenceObservationId, DateTimeOffset ObservedAtUtc,
    DateTimeOffset WindowFromUtc, DateTimeOffset WindowToUtc, string Source, string Origin, string Status,
    ReportSoilMoisturePeriod? Before, ReportSoilMoisturePeriod? After, decimal? DeltaPercentagePoints,
    string Assessment, string NextCheck, string Direction, string LevelBefore, string LevelAfter,
    ReportSoilMoistureThreshold Threshold);

public sealed record ReportSoilMoistureThreshold(string Status, decimal? MinPercent, decimal? MaxPercent);
