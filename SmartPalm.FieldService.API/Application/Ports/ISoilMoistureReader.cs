namespace SmartPalm.FieldService.Application.Ports;

public interface ISoilMoistureReader
{
    Task<SoilMoistureReadResult> GetAsync(long plantationId, long sectorId, DateTimeOffset from, DateTimeOffset to, CancellationToken token);
    Task<SoilMoistureThresholdResult> GetThresholdAsync(SoilMoistureInstrument instrument, CancellationToken token);
}

public record SoilMoistureReadResult(bool IsAvailable, IReadOnlyList<SoilMoistureSample> Samples);
public record SoilMoistureInstrument(string EdgeMac, string IotMac);
public record SoilMoistureSample(string DeviceKey, DateTimeOffset MeasuredAt, decimal Value, SoilMoistureInstrument? Instrument = null);
public enum SoilMoistureThresholdStatus { Configured, Missing, Denied, Unavailable, Invalid, NotEvaluated }
public record SoilMoistureThresholdResult(SoilMoistureThresholdStatus Status, decimal? MinPercent = null, decimal? MaxPercent = null);
