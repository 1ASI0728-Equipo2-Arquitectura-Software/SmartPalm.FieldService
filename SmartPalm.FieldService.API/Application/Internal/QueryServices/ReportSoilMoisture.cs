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
        var direction = delta < 0 ? "Falling" : delta > 0 ? "Rising" : "Unchanged";
        var instrument = comparison.Before[0].Instrument;
        var reading = instrument is not null && comparison.Before.Concat(comparison.After).All(sample => sample.Instrument == instrument)
            ? await reader.GetThresholdAsync(instrument, token) : new(SoilMoistureThresholdStatus.Unavailable);
        var threshold = ToThreshold(reading);
        var levelBefore = Level(before.MedianPercent, threshold);
        var levelAfter = Level(after.MedianPercent, threshold);
        var levelText = threshold.MinPercent is { } min && threshold.MaxPercent is { } max
            ? $"Frente al rango vigente [{FormatThreshold(min)} %, {FormatThreshold(max)} %], las medianas estuvieron {Describe(levelBefore)} y {Describe(levelAfter)}, respectivamente. "
            : "Nivel desconocido: no se obtuvo un rango vigente, válido y autorizado. ";
        var assessment = $"Sector {sector}: mediana de humedad de {Format(before.MedianPercent)} % (n={before.SampleCount}) a {Format(after.MedianPercent)} % (n={after.SampleCount}); "
            + $"diferencia: {Format(delta)} puntos porcentuales. {levelText}Estos datos no prueban una tendencia sostenida ni la causa en el punto observado.";
        var next = levelAfter switch
        {
            "BelowRange" => "confirmar el nivel por debajo del mínimo con una medición independiente en el punto observado y contrastarlo con el registro de riego."
                + (delta > 0 ? " La subida de la mediana no elimina que siga por debajo del rango vigente." : string.Empty),
            "AboveRange" => "confirmar el nivel por encima del máximo con una medición independiente en el punto observado y revisar la cobertura del sensor; no inferir encharcamiento.",
            "WithinRange" when delta < 0 => "comparar la humedad del punto observado con la de un punto cercano y repetir la medición: la mediana bajó, pero sigue dentro del rango vigente.",
            "WithinRange" => "comparar la humedad del punto observado con la de un punto cercano: la mediana no bajó y está dentro del rango vigente; esto no descarta un problema localizado.",
            _ => "verificar el rango configurado para el mismo sensor y repetir mediciones en el punto observado antes de clasificar el nivel."
        };
        return Context("Compared", assessment, $"Sector {sector}: {next}", before, after, delta, direction, levelBefore, levelAfter, threshold);

        ReportSoilMoistureContext Context(string status, string text, string check, ReportSoilMoisturePeriod? before = null,
            ReportSoilMoisturePeriod? after = null, decimal? delta = null, string direction = "Unknown", string levelBefore = "Unknown",
            string levelAfter = "Unknown", ReportSoilMoistureThreshold? threshold = null) => new(sector, observation.ObservationId,
            anchor, from, to, "Ingestion", "Unverified", status, before, after, delta, text, check, direction, levelBefore, levelAfter,
            threshold ?? new("NotEvaluated", null, null));
    }

    private static ReportSoilMoisturePeriod Summarize(IReadOnlyList<SoilMoistureSample> samples)
    {
        var sorted = samples.Select(sample => sample.Value).Order().ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
        return new(sorted.Length, Math.Round(median, 2, MidpointRounding.AwayFromZero),
            samples.Min(sample => sample.MeasuredAt).ToUniversalTime(), samples.Max(sample => sample.MeasuredAt).ToUniversalTime());
    }

    private static ReportSoilMoistureThreshold ToThreshold(SoilMoistureThresholdResult reading) => reading switch
    {
        { Status: SoilMoistureThresholdStatus.Configured, MinPercent: { } min, MaxPercent: { } max }
            when min >= 0 && max <= 100 && min <= max => new("Configured", min, max),
        { Status: SoilMoistureThresholdStatus.Configured } => new("Invalid", null, null),
        _ => new(reading.Status.ToString(), null, null)
    };

    private static string Level(decimal median, ReportSoilMoistureThreshold threshold) =>
        threshold.MinPercent is { } min && threshold.MaxPercent is { } max
            ? median < min ? "BelowRange" : median > max ? "AboveRange" : "WithinRange"
            : "Unknown";

    private static string Describe(string level) => level switch
    {
        "BelowRange" => "por debajo del mínimo",
        "AboveRange" => "por encima del máximo",
        _ => "dentro del rango"
    };

    private static string Format(decimal value) => value.ToString("0.##", Spanish);
    private static string FormatThreshold(decimal value) => value.ToString("0.############################", Spanish);
}
