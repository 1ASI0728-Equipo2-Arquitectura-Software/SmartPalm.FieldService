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
        var context = contexts.HttpContext;
        var headers = context?.Request.Headers.Authorization;
        if (plantationId <= 0 || sectorId <= 0 || from > to || context?.User.Identity?.IsAuthenticated != true ||
            headers is not { Count: 1 } || !AuthenticationHeaderValue.TryParse(headers.Value[0], out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            authorization.Parameter is not { } jwt || !Regex.IsMatch(jwt, @"\A[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\z") ||
            !Uri.TryCreate(configuration["Ingestion:BaseUrl"], UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)) return Unavailable;

        var path = $"api/v1/sectors/{sectorId}/sensor-readings?from={Uri.EscapeDataString(from.ToUniversalTime().ToString("O"))}&to={Uri.EscapeDataString(to.ToUniversalTime().ToString("O"))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
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
                samples.Add(new SoilMoistureSample($"{edge.Length}:{edge}{device}", measured, value));
            }
            return new SoilMoistureReadResult(true, samples);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Unavailable; }
        catch (HttpRequestException) { return Unavailable; }
        catch (JsonException) { return Unavailable; }
        catch (IOException) { return Unavailable; }
    }

    private sealed record Row(string? EdgeDeviceMacAddress, string? IotDeviceMacAddress, string? SensorType,
        string? Unit, DateTimeOffset? MeasuredAt, decimal? Value, long? PlantationId, long? SectorId);
}
