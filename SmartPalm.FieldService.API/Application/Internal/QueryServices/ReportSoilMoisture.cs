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
        const string nextWithoutComparison = "Empieza por revisar si el sensor cubre la zona observada y por conseguir mediciones cercanas al punto y al momento de la visita.\n\nCon esas lecturas podrás contrastar la nota antes de intentar explicar la causa.";
        var result = await reader.GetAsync(plantationId, sector, from, to, token);
        if (!result.IsAvailable)
            return Context("Unavailable", $"Sector {sector}: la observación de la visita está registrada, pero no pudimos consultar las lecturas de humedad en Ingestion.\n\nPor ahora no podemos contrastar lo observado con las lecturas del sector ni decir si la humedad cambió.", nextWithoutComparison);

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
            return Context("InsufficientData", $"Sector {sector}: no hay suficientes lecturas para contrastar la observación de la visita.\n\nPara hacer una comparación se necesitan al menos tres mediciones distintas del mismo sensor antes y tres desde el momento observado. Sin ellas, no es posible concluir si la humedad subió o bajó.",
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
            ? $"El rango vigente del sensor es de {FormatThreshold(min)} % a {FormatThreshold(max)} %. "
                + (levelBefore == levelAfter
                    ? $"Ambas medianas quedaron {Describe(levelAfter)}."
                    : $"La primera mediana quedó {Describe(levelBefore)} y la segunda, {Describe(levelAfter)}.")
            : "No contamos con un rango vigente válido y autorizado para este sensor. Podemos describir el cambio, pero no clasificar el nivel de humedad.";
        var change = delta switch
        {
            > 0 => $"La mediana subió {Format(delta)} puntos porcentuales.",
            < 0 => $"La mediana bajó {Format(-delta)} puntos porcentuales.",
            _ => "La mediana no cambió entre las dos ventanas."
        };
        var assessment = $"Sector {sector}: en el mismo sensor, la humedad mediana pasó de {Format(before.MedianPercent)} % ({before.SampleCount} lecturas) "
            + $"a {Format(after.MedianPercent)} % ({after.SampleCount} lecturas). {change}\n\n"
            + $"{levelText} La comparación orienta la revisión, pero no confirma la causa en el punto observado ni una tendencia sostenida.";
        var next = levelAfter switch
        {
            "BelowRange" => "Primero, confirma con una medición independiente si el nivel por debajo del mínimo también se observa en el punto de la visita. Luego, contrasta esa medición con el registro de riego.\n\nEsto ayuda a orientar la revisión sin dar por hecha una falla de riego."
                + (delta > 0 ? " La subida observada no cambia que la mediana siga por debajo del rango." : string.Empty),
            "AboveRange" => "Comprueba con una medición independiente si el nivel por encima del máximo también se registra en el punto observado. Revisa además qué zona cubre el sensor.\n\nEsto permite contrastar la lectura sin concluir que hubo encharcamiento.",
            "WithinRange" when delta < 0 => "Compara la humedad del punto observado con la de un punto cercano y repite la medición.\n\nLa mediana bajó, pero sigue dentro del rango vigente. Revisar ambos puntos ayuda a saber si la diferencia se limita a la zona observada.",
            "WithinRange" => "Compara la humedad del punto observado con la de un punto cercano.\n\nLa mediana no bajó y está dentro del rango vigente. Eso no descarta una condición localizada: la comparación entre ambos puntos ayuda a precisar dónde seguir revisando.",
            _ => "Verifica el rango configurado para el mismo sensor y repite mediciones en el punto observado.\n\nEl cambio entre lecturas no basta para decir si el nivel es bajo o alto. Con un rango válido podrás interpretar esas mediciones sin asumir un umbral."
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
