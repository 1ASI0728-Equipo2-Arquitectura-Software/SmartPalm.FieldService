using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartPalm.FieldService.Infrastructure.ExternalServices;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class IngestionSoilMoistureReaderTests
{
    private static readonly DateTimeOffset From = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset To = From.AddDays(1);
    private const string Row = "{\"edgeDeviceMacAddress\":\"edge\",\"iotDeviceMacAddress\":\"device\",\"sensorType\":\"SoilMoisture\",\"unit\":\"Percent\",\"measuredAt\":\"2026-01-01T00:00:00Z\",\"value\":0,\"plantationId\":1,\"sectorId\":2}";

    [Fact]
    public async Task ForwardsCallerAndFiltersRowsWithinInclusiveBounds()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + Row + "," + Row.Replace("2026-01-01", "2026-01-02") + "," + Row.Replace("\"sectorId\":2", "\"sectorId\":3") + "," + Row.Replace("SoilMoisture", "Temperature") + "]") });
        var result = await Reader(handler).GetAsync(1, 2, From, To, default);
        Assert.True(result.IsAvailable);
        Assert.Equal(2, result.Samples.Count);
        Assert.Equal(0m, result.Samples[0].Value);
        Assert.Equal(To, result.Samples[1].MeasuredAt);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("Bearer caller.jwt.token", handler.Request.Headers.Authorization!.ToString());
        Assert.Equal("https://ingestion.test/api/v1/sectors/2/sensor-readings?from=" + Uri.EscapeDataString(From.ToString("O")) + "&to=" + Uri.EscapeDataString(To.ToString("O")), handler.Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task FiltersWrongScopeSensorUnitTimeAndMissingValuesOrDeviceKeys()
    {
        var rows = new[] { Row.Replace("\"plantationId\":1", "\"plantationId\":9"), Row.Replace("\"sectorId\":2", "\"sectorId\":9"),
            Row.Replace("SoilMoisture", "Temperature"), Row.Replace("Percent", "Celsius"),
            Row.Replace("2026-01-01", "2025-12-31"), Row.Replace("2026-01-01", "2026-01-03"),
            Row.Replace("\"value\":0,", ""), Row.Replace("\"value\":0", "\"value\":-1"), Row.Replace("\"value\":0", "\"value\":101"),
            Row.Replace("\"iotDeviceMacAddress\":\"device\"", "\"iotDeviceMacAddress\":\" \""),
            Row.Replace("\"edgeDeviceMacAddress\":\"edge\"", "\"edgeDeviceMacAddress\":null"), Row.Replace("\"measuredAt\":\"2026-01-01T00:00:00Z\",", ""),
            Row.Replace("\"sectorId\":2", "\"sectorId\":0"), Row.Replace("\"plantationId\":1,", "") };
        var result = await Reader(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + string.Join(',', rows) + "]") })).GetAsync(1, 2, From, To, default);
        Assert.True(result.IsAvailable); Assert.Empty(result.Samples);
    }

    [Fact]
    public async Task RejectsMissingOrMultipleOrNonBearerCredentials()
    {
        foreach (var header in new[] { "", "Basic abc", "Bearer bad", "Bearer a.b.c, Bearer d.e.f" })
        {
            var handler = new Handler(_ => throw new Exception("must not send"));
            var reader = Reader(handler, authorization: header);
            var result = await reader.GetAsync(1, 2, From, To, default);
            Assert.False(result.IsAvailable); Assert.Empty(result.Samples); Assert.Null(handler.Request);
        }
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[{\"value\":\"not-a-number\"}]")]
    public async Task MalformedResponsesAreUnavailable(string body)
    {
        var result = await Reader(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })).GetAsync(1, 2, From, To, default);
        Assert.False(result.IsAvailable); Assert.Empty(result.Samples);
    }

    [Fact]
    public async Task EmptyIsAvailableButCapAndErrorsAreUnavailable()
    {
        foreach (var (body, status, available) in new[] { ("[]", HttpStatusCode.OK, true), ("[" + string.Join(',', Enumerable.Repeat(Row, 10000)) + "]", HttpStatusCode.OK, false), ("[]", HttpStatusCode.Forbidden, false), ("[]", HttpStatusCode.Redirect, false) })
        {
            var result = await Reader(new Handler(_ => new HttpResponseMessage(status) { Content = new StringContent(body) })).GetAsync(1, 2, From, To, default);
            Assert.Equal(available, result.IsAvailable); Assert.Empty(result.Samples);
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("https://ingestion.test/path", true)]
    [InlineData("https://user@ingestion.test", true)]
    [InlineData("https://ingestion.test?x=1", true)]
    [InlineData("https://ingestion.test", false)]
    public async Task RejectsInvalidConfigurationOrCallerWithoutHttp(string? origin, bool authenticated)
    {
        var handler = new Handler(_ => throw new Exception("must not send"));
        var result = await Reader(handler, origin, authenticated).GetAsync(1, 2, From, To, default);
        Assert.False(result.IsAvailable); Assert.Empty(result.Samples); Assert.Null(handler.Request);
    }

    [Fact]
    public async Task CancellationIsPropagatedButTimeoutDegrades()
    {
        var handler = new Handler(_ => throw new TaskCanceledException());
        Assert.False((await Reader(handler).GetAsync(1, 2, From, To, default)).IsAvailable);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(handler).GetAsync(1, 2, From, To, cts.Token));
    }

    private static IngestionSoilMoistureReader Reader(Handler handler, string? origin = "https://ingestion.test", bool authenticated = true, string authorization = "Bearer caller.jwt.token")
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(authenticated ? "test" : null)) };
        context.Request.Headers.Authorization = authorization;
        return new IngestionSoilMoistureReader(new Factory(handler), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ingestion:BaseUrl"] = origin }).Build(), new HttpContextAccessor { HttpContext = context });
    }
    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) { Assert.Equal(IngestionSoilMoistureReader.ClientName, name); return new HttpClient(handler, false); }
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Request = request; return Task.FromResult(send(request)); }
    }
}
