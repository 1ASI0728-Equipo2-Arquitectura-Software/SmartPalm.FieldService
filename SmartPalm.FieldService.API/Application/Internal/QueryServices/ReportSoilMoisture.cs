using System.Globalization;
using SmartPalm.FieldService.Application.Ports;

namespace SmartPalm.FieldService.Application.QueryServices;

internal static class ReportSoilMoisture
{
    public const int MaxSectors = 3;
    private const int MinSamplesPerPeriod = 3;
    private static readonly long HalfWindowTicks = TimeSpan.FromHours(2).Ticks;
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es");

    public static async Task<ReportSoilMoistureContext> CompareAsync(
        ISoilMoistureReader reader, long plantationId, ReportDraftSource observation, CancellationToken token)
    {
        var sector = observation.SectorId;
        var anchor = new DateTimeOffset(DateTime.SpecifyKind(observation.ObservedAtUtc, DateTimeKind.Utc));
        var from = new DateTimeOffset(Math.Max(0, anchor.UtcTicks - HalfWindowTicks), TimeSpan.Zero);
        var to = new DateTimeOffset(Math.Min(DateTimeOffset.MaxValue.UtcTicks, anchor.UtcTicks + HalfWindowTicks), TimeSpan.Zero);
        const string nextWithoutComparison = "Verificar la cobertura del sensor y obtener mediciones cercanas al momento y al punto observados antes de atribuir una causa.";
        var result = await reader.GetAsync(plantationId, sector, from, to, token);
        if (!result.IsAvailable)
            return Context("Unavailable", $"Sector {sector}: no se pudo consultar la humedad del suelo en Ingestion.", nextWithoutComparison);

        // Compare one instrument over time; discard timestamps with conflicting values and do not weight duplicates.
        var comparison = result.Samples.GroupBy(sample => sample.DeviceKey, StringComparer.Ordinal)
            .Select(device =>
            {
                var unique = device.GroupBy(sample => sample.MeasuredAt)
                    .Where(atTime => atTime.Select(sample => sample.Value).Distinct().Skip(1).Any() == false)
                    .Select(atTime => atTime.First()).ToArray();
                return new
                {
                    Device = device.Key,
                    Before = unique.Where(sample => sample.MeasuredAt < anchor).ToArray(),
                    After = unique.Where(sample => sample.MeasuredAt >= anchor).ToArray()
                };
            })
            .Where(series => series.Before.Length >= MinSamplesPerPeriod && series.After.Length >= MinSamplesPerPeriod)
            .OrderByDescending(series => Math.Min(series.Before.Length, series.After.Length))
            .ThenByDescending(series => series.Before.Length + series.After.Length)
            .ThenBy(series => series.Device, StringComparer.Ordinal)
            .FirstOrDefault();
        if (comparison is null)
            return Context("InsufficientData", $"Sector {sector}: faltan al menos tres mediciones distintas de un mismo sensor a cada lado de la observación para hacer el contraste.",
                nextWithoutComparison);

        var before = Summarize(comparison.Before);
        var after = Summarize(comparison.After);
        var delta = after.MedianPercent - before.MedianPercent;
        var assessment = $"Sector {sector}: la mediana de humedad de un mismo sensor pasó de {Format(before.MedianPercent)} % "
            + $"(n={before.SampleCount}) a {Format(after.MedianPercent)} % (n={after.SampleCount}); diferencia: {Format(delta)} puntos porcentuales. "
            + "La comparación no establece la causa ni mide necesariamente el punto observado.";
        var next = delta < 0
            ? $"Sector {sector}: verificar si el descenso también aparece en otros puntos y contrastar las mediciones con el registro de riego."
            : $"Sector {sector}: comparar la humedad del punto observado con la de un punto cercano; la mediana del sensor no disminuyó.";
        return Context("Compared", assessment, next, before, after, delta);

        ReportSoilMoistureContext Context(string status, string text, string check, ReportSoilMoisturePeriod? before = null,
            ReportSoilMoisturePeriod? after = null, decimal? delta = null) => new(sector, observation.ObservationId,
            anchor, from, to, "Ingestion", "Unverified", status, before, after, delta, text, check);
    }

    private static ReportSoilMoisturePeriod Summarize(IReadOnlyList<SoilMoistureSample> samples)
    {
        var sorted = samples.Select(sample => sample.Value).Order().ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
        return new(sorted.Length, Math.Round(median, 2, MidpointRounding.AwayFromZero),
            samples.Min(sample => sample.MeasuredAt).ToUniversalTime(), samples.Max(sample => sample.MeasuredAt).ToUniversalTime());
    }

    private static string Format(decimal value) => value.ToString("0.##", Spanish);
}
