namespace SmartPalm.FieldService.Application.Ports;

public interface ISoilMoistureReader
{
    Task<SoilMoistureReadResult> GetAsync(long plantationId, long sectorId, DateTimeOffset from, DateTimeOffset to, CancellationToken token);
}

public record SoilMoistureReadResult(bool IsAvailable, IReadOnlyList<SoilMoistureSample> Samples);
public record SoilMoistureSample(string DeviceKey, DateTimeOffset MeasuredAt, decimal Value);
