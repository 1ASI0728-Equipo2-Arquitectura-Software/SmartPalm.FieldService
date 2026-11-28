namespace SmartPalm.FieldService.Application.Ports;

public interface IReportDraftProvider
{
    Task<ReportDraftContent> GenerateAsync(IReadOnlyList<ReportDraftSource> evidence, CancellationToken token);
}

public interface IReportDraftEvidenceRepository
{
    Task<IReadOnlyList<ReportDraftSource>> ListAsync(long visitId, long plantationId, int limit, int descriptionLimit, CancellationToken token);
}

public sealed record ReportDraftSource(long ObservationId, long InspectionId, long SectorId, DateTime ObservedAtUtc, string Description);
public sealed record ReportDraftFinding(long SectorId, string Text, long[] ObservationIds);
public sealed record ReportDraftContent(string Title, ReportDraftFinding[] Findings, string[] Interpretation, string[] OutstandingChecks);
public sealed record ReportDraftVisit(long Id, string Status);
public sealed record ReportDraftResult(ReportDraftVisit Visit, string Title, ReportDraftFinding[] Findings,
    string[] Interpretation, string[] OutstandingChecks, IReadOnlyList<ReportDraftSource> Sources, IReadOnlyList<string> Limitations);
