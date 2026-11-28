using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Application.Ports;

namespace SmartPalm.FieldService.Infrastructure.Persistence;

internal sealed class ReportDraftEvidenceRepository(FieldDbContext db) : IReportDraftEvidenceRepository
{
    public async Task<IReadOnlyList<ReportDraftSource>> ListAsync(long visitId, long plantationId, int limit,
        int descriptionLimit, CancellationToken token) => await db.Inspections.AsNoTracking()
        .Where(inspection => inspection.VisitId == visitId && inspection.PlantationId == plantationId)
        .SelectMany(inspection => inspection.Observations.Where(observation => observation.Description.Trim() != ""),
            (inspection, observation) => new { inspection, observation })
        .OrderByDescending(row => row.observation.ObservedAtUtc).ThenByDescending(row => row.observation.Id)
        .Take(limit)
        .Select(row => new ReportDraftSource(row.observation.Id, row.inspection.Id, row.inspection.SectorId,
            row.observation.ObservedAtUtc, row.observation.Description.Length > descriptionLimit
                ? row.observation.Description.Substring(0, descriptionLimit) : row.observation.Description))
        .ToArrayAsync(token);
}
