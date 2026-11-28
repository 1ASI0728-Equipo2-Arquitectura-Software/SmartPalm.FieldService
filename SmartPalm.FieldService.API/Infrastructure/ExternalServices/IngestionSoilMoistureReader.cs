using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartPalm.FieldService.Application.Ports;

namespace SmartPalm.FieldService.Infrastructure.ExternalServices;

public sealed class IngestionSoilMoistureReader(
    IHttpClientFactory clients, IConfiguration configuration, IHttpContextAccessor contexts) : ISoilMoistureReader
{
    public const string ClientName = "IngestionSoilMoisture";
    private static readonly SoilMoistureReadResult Unavailable = new(false, Array.Empty<SoilMoistureSample>());
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SoilMoistureReadResult> GetAsync(long plantationId, long sectorId, DateTimeOffset from, DateTimeOffset to, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (plantationId <= 0 || sectorId <= 0 || from > to) return Unavailable;
        var path = $"api/v1/sectors/{sectorId}/sensor-readings?from={Uri.EscapeDataString(from.ToUniversalTime().ToString("O"))}&to={Uri.EscapeDataString(to.ToUniversalTime().ToString("O"))}";
        using var request = CreateRequest(path);
        if (request is null) return Unavailable;
        try
        {
            using var client = clients.CreateClient(ClientName);
            using var response = await client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return Unavailable;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var rows = await JsonSerializer.DeserializeAsync<Row[]>(stream, JsonOptions, token);
            if (rows is null || rows.Length >= 10000) return Unavailable;
            var samples = new List<SoilMoistureSample>();
            foreach (var row in rows)
            {
                if (row is null || row.PlantationId != plantationId || row.SectorId != sectorId ||
                    row.SensorType != "SoilMoisture" || row.Unit != "Percent" ||
                    row.MeasuredAt is not { } measured || measured < from || measured > to || measured == default ||
                    row.Value is not { } value || value < 0 || value > 100 ||
                    string.IsNullOrWhiteSpace(row.EdgeDeviceMacAddress) || string.IsNullOrWhiteSpace(row.IotDeviceMacAddress)) continue;
                var edge = row.EdgeDeviceMacAddress.Trim().ToUpperInvariant();
                var device = row.IotDeviceMacAddress.Trim().ToUpperInvariant();
                samples.Add(new SoilMoistureSample($"{edge.Length}:{edge}{device}", measured, value, new SoilMoistureInstrument(edge, device)));
            }
            return new SoilMoistureReadResult(true, samples);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Unavailable; }
        catch (HttpRequestException) { return Unavailable; }
        catch (JsonException) { return Unavailable; }
        catch (IOException) { return Unavailable; }
    }

    public async Task<SoilMoistureThresholdResult> GetThresholdAsync(SoilMoistureInstrument instrument, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        static SoilMoistureThresholdResult Result(SoilMoistureThresholdStatus status) => new(status);
        if (instrument is null || !IsSafeMac(instrument.EdgeMac) || !IsSafeMac(instrument.IotMac))
            return Result(SoilMoistureThresholdStatus.Unavailable);
        using var request = CreateRequest($"api/v1/devices/{Uri.EscapeDataString(instrument.IotMac)}/thresholds");
        if (request is null) return Result(SoilMoistureThresholdStatus.Unavailable);
        try
        {
            using var client = clients.CreateClient(ClientName);
            using var response = await client.SendAsync(request, token);
            if (response.StatusCode == HttpStatusCode.NotFound) return Result(SoilMoistureThresholdStatus.Missing);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return Result(SoilMoistureThresholdStatus.Denied);
            if (!response.IsSuccessStatusCode) return Result(SoilMoistureThresholdStatus.Unavailable);
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return Result(SoilMoistureThresholdStatus.Invalid);
            ThresholdRow? found = null;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) return Result(SoilMoistureThresholdStatus.Invalid);
                if (!item.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "SoilMoisture") continue;
                var row = item.Deserialize<ThresholdRow>(JsonOptions);
                if (found is not null || row is null || !string.Equals(row.EdgeMac, instrument.EdgeMac, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(row.IotMac, instrument.IotMac, StringComparison.OrdinalIgnoreCase) ||
                    row.Min is not { } min || row.Max is not { } max || min < 0 || max > 100 || min > max)
                    return Result(SoilMoistureThresholdStatus.Invalid);
                found = row;
            }
            return found is null ? Result(SoilMoistureThresholdStatus.Missing) : new(SoilMoistureThresholdStatus.Configured, found.Min, found.Max);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Result(SoilMoistureThresholdStatus.Unavailable); }
        catch (HttpRequestException) { return Result(SoilMoistureThresholdStatus.Unavailable); }
        catch (IOException) { return Result(SoilMoistureThresholdStatus.Unavailable); }
        catch (JsonException) { return Result(SoilMoistureThresholdStatus.Invalid); }
    }

    private static bool IsSafeMac(string? mac) => mac is not null && Regex.IsMatch(mac, @"\A[A-Za-z0-9:-]+\z");

    private HttpRequestMessage? CreateRequest(string path)
    {
        var context = contexts.HttpContext;
        var headers = context?.Request.Headers.Authorization;
        if (context?.User.Identity?.IsAuthenticated != true || headers is not { Count: 1 } ||
            !AuthenticationHeaderValue.TryParse(headers.Value[0], out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            authorization.Parameter is not { } jwt || !Regex.IsMatch(jwt, @"\A[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\z") ||
            !Uri.TryCreate(configuration["Ingestion:BaseUrl"], UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)) return null;
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return request;
    }

    private sealed record ThresholdRow(string? EdgeMac, string? IotMac, decimal? Min, decimal? Max);
    private sealed record Row(string? EdgeDeviceMacAddress, string? IotDeviceMacAddress, string? SensorType,
        string? Unit, DateTimeOffset? MeasuredAt, decimal? Value, long? PlantationId, long? SectorId);
}
