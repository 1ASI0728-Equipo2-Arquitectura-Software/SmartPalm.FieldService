using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Application.QueryServices;

namespace SmartPalm.FieldService.Infrastructure.ExternalServices;

public sealed class MiniMaxReportDraftProvider(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<MiniMaxReportDraftProvider> logger) : IReportDraftProvider
{
    public const string ClientName = "ReportDraftMiniMax";
    private const string Endpoint = "https://api.minimax.io/v1/chat/completions";
    private const string InstructionResource = "SmartPalm.FieldService.Skills.visit-report-draft.SKILL.md";
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions DraftJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 8
    };

    public async Task<ReportDraftContent> GenerateAsync(IReadOnlyList<ReportDraftSource> evidence, CancellationToken token)
    {
        var key = configuration["MINIMAX_API_KEY"];
        var model = configuration["MINIMAX_MODEL"];
        if (string.IsNullOrWhiteSpace(key)) throw Unavailable("configuration");
        if (string.IsNullOrWhiteSpace(model)) model = "MiniMax-M3.1-Flash";
        using var instructions = typeof(MiniMaxReportDraftProvider).Assembly.GetManifestResourceStream(InstructionResource)
            ?? throw Unavailable("instruction-resource");
        using var reader = new StreamReader(instructions);
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        request.Content = JsonContent.Create(new
        {
            model,
            reasoning_effort = "low",
            reasoning_split = true,
            max_completion_tokens = 3072,
            messages = new[]
            {
                new { role = "system", content = await reader.ReadToEndAsync(token) },
                new { role = "user", content = JsonSerializer.Serialize(evidence.Select(source => new
                {
                    source.ObservationId, source.SectorId, source.ObservedAtUtc, source.Description
                }), WireJson) }
            }
        });

        try
        {
            using var response = await client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) throw Unavailable("upstream-http");
            var envelope = await response.Content.ReadFromJsonAsync<MiniMaxEnvelope>(WireJson, token);
            if (envelope?.BaseResponse is { StatusCode: not 0 }) throw Unavailable("upstream-status");
            if (envelope?.Choices?.FirstOrDefault() is not { FinishReason: "stop", Message.Content: { Length: > 0 } content })
                throw Unavailable("incomplete-output");
            return JsonSerializer.Deserialize<ReportDraftContent>(content, DraftJson) ?? throw Unavailable("null-draft");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            logger.LogWarning("Report draft provider timed out.");
            throw new ReportDraftException(ReportDraftFailure.ProviderTimeout);
        }
        catch (HttpRequestException) { throw Unavailable("transport"); }
        catch (JsonException) { throw Unavailable("invalid-json"); }
    }

    private ReportDraftException Unavailable(string reason)
    {
        logger.LogWarning("Report draft provider failed. Category={Category}", reason);
        return new ReportDraftException(ReportDraftFailure.ProviderUnavailable);
    }

    private sealed record MiniMaxEnvelope([property: JsonPropertyName("base_resp")] MiniMaxStatus? BaseResponse, MiniMaxChoice[]? Choices);
    private sealed record MiniMaxStatus([property: JsonPropertyName("status_code")] int StatusCode);
    private sealed record MiniMaxChoice([property: JsonPropertyName("finish_reason")] string? FinishReason, MiniMaxMessage? Message);
    private sealed record MiniMaxMessage(string? Content);
}
