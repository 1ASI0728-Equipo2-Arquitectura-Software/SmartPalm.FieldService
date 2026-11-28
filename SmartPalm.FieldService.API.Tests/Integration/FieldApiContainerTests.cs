using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartPalm.FieldService.Infrastructure.ExternalServices;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
namespace SmartPalm.FieldService.API.Tests.Integration;
public class FieldApiContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("smartpalm_field").WithUsername("smartpalm").WithPassword("tests").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    public async Task InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());
    public async Task DisposeAsync() => await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    [DockerRequiredFact, Trait("Category", "Integration")]
    public async Task HealthAndInbox_WorkWithPostgreSqlAndRabbitMq()
    {
        using var factory = CreateFactory(); using var client = factory.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        await using var connection = await new ConnectionFactory { Uri = new Uri(_rabbit.GetConnectionString()) }.CreateConnectionAsync(); await using var channel = await connection.CreateChannelAsync(); await channel.ExchangeDeclareAsync("smartpalm.events", ExchangeType.Topic, true, false);
        var id = Guid.NewGuid(); var body = JsonSerializer.SerializeToUtf8Bytes(new { eventId = id, eventType = "smartpalm.identity.user-created.v1", payload = new { userId = 77, username = "field-user", email = "field@test.local", fullName = "Field User", role = "Agronomist", occurredAtUtc = DateTime.UtcNow } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        for (var attempt = 0; attempt < 20; attempt++) { await channel.BasicPublishAsync("smartpalm.events", "smartpalm.identity.user-created.v1", false, new BasicProperties { Persistent = true, MessageId = id.ToString(), Type = "smartpalm.identity.user-created.v1" }, body); await Task.Delay(300); if (await ScalarAsync("SELECT COUNT(*) FROM field.inbox_messages") == 1) break; }
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.user_projections WHERE user_id = 77")); Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.inbox_messages"));

        async Task PublishAsync(string type, object payload, string sql, bool wrapped = true)
        {
            var eventId = Guid.NewGuid();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(wrapped ? new { eventId, eventType = type, payload } : payload);
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await channel.BasicPublishAsync("smartpalm.events", type, false, new BasicProperties { Persistent = true, MessageId = eventId.ToString(), Type = type }, bytes);
                await Task.Delay(300);
                if (await ScalarAsync(sql) == 1) return;
            }
            Assert.Fail($"Event {type} was not projected.");
        }
        var now = DateTime.UtcNow;
        await PublishAsync("crop.plantation-created.v1", new { plantationId = 11, palmGrowerId = 12, name = "Farm", hectares = 10, occurredAtUtc = now }, "SELECT COUNT(*) FROM field.plantation_projections WHERE plantation_id = 11");
        await PublishAsync("crop.sector-assigned.v1", new { sectorId = 99, plantationId = 11, deviceMacAddress = "AA", name = "Sector", occurredAtUtc = now }, "SELECT COUNT(*) FROM field.sector_projections WHERE sector_id = 99");
        await PublishAsync("crop.agronomist-affiliated.v1", new { affiliationId = 33, agronomistId = 77, plantationId = 11, occurredAtUtc = now }, "SELECT COUNT(*) FROM field.agronomist_affiliation_projections WHERE agronomist_id = 77 AND plantation_id = 11 AND active");
        await PublishAsync("alert.triggered.v1", new { alertId = 44, userId = 77, plantationId = 11, sectorId = 99, gatewayMac = "AA", deviceMac = "BB", sensorType = "temperature", value = 5, min = 0, max = 4, level = "High", message = "Alert", occurredAtUtc = now }, "SELECT COUNT(*) FROM field.alert_projections WHERE alert_id = 44", false);

        await PublishAsync("smartpalm.identity.user-created.v1", new { userId = 78, role = "Administrator", occurredAtUtc = now }, "SELECT COUNT(*) FROM field.user_projections WHERE user_id = 78", false);
        string Token(long id, string role) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("tests", "tests", [new Claim("sub", id.ToString()), new Claim("role", role)], expires: now.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("field-tests-signing-key-with-at-least-32-characters")), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(77, "Agronomist"));
        var visit = await client.PostAsJsonAsync("/api/v1/field/visits", new { plantationId = 11, agronomistId = 77, scheduledDate = DateOnly.FromDateTime(now.AddDays(1)), objectives = "Inspect" });
        Assert.Equal(HttpStatusCode.Created, visit.StatusCode);
        var visitId = (await visit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/field/visits/{visitId}/start", new { agronomistId = 77 })).StatusCode);
        var inspection = await client.PostAsJsonAsync($"/api/v1/field/visits/{visitId}/inspections", new { clientReference = Guid.NewGuid(), sectorId = 99, agronomistId = 77, observations = Array.Empty<object>(), inspectedAt = now, linkedAlertIds = new[] { 44 } });
        Assert.Equal(HttpStatusCode.Created, inspection.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/field/visits/{visitId}/complete", new { agronomistId = 77 })).StatusCode);
        var request = new { plantationId = 11, sectorId = 99, registeredBy = 77, performedBy = "Worker", type = "Other", description = "Treatment", executionDate = now };
        var intervention = await client.PostAsJsonAsync("/api/v1/field/interventions", request);
        Assert.Equal(HttpStatusCode.Created, intervention.StatusCode);
        var interventionId = (await intervention.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/field/interventions/{interventionId}/verify", new { agronomistId = 77, notes = "Verified" })).StatusCode);
        var before = await ScalarAsync("SELECT COUNT(*) FROM field.agronomic_interventions");
        var outboxBefore = await ScalarAsync("SELECT COUNT(*) FROM field.outbox_messages");
        var spoof = await client.PostAsJsonAsync("/api/v1/field/interventions", new { plantationId = 11, sectorId = 99, registeredBy = 88, performedBy = "Worker", type = "Other", description = "Spoof", executionDate = now });
        Assert.Equal(HttpStatusCode.Forbidden, spoof.StatusCode);
        Assert.Equal(before, await ScalarAsync("SELECT COUNT(*) FROM field.agronomic_interventions"));
        Assert.Equal(outboxBefore, await ScalarAsync("SELECT COUNT(*) FROM field.outbox_messages"));

        await PublishAsync("smartpalm.identity.user-created.v1", new { userId = 79, role = "Agronomist", occurredAtUtc = now }, "SELECT COUNT(*) FROM field.user_projections WHERE user_id = 79");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(79, "Agronomist"));
        var unaffiliatedVisit = new { plantationId = 11, agronomistId = 79, scheduledDate = DateOnly.FromDateTime(now.AddDays(1)), objectives = "Inspect" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/field/visits", unaffiliatedVisit)).StatusCode);

        await PublishAsync("smartpalm.identity.user-created.v1", new { userId = 12, role = "PalmGrower", occurredAtUtc = now }, "SELECT COUNT(*) FROM field.user_projections WHERE user_id = 12");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(12, "PalmGrower"));
        var growerRequest = new { plantationId = 11, sectorId = 99, registeredBy = 12, performedBy = "Worker", type = "Other", description = "Treatment", executionDate = now };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/field/interventions", growerRequest)).StatusCode);
        await PublishAsync("smartpalm.identity.subscription-activated.v1", new { subscriptionId = 1, userId = 12, planType = "Standard", startDate = now, endDate = now.AddDays(30), occurredAtUtc = now }, "SELECT COUNT(*) FROM field.user_projections WHERE user_id = 12 AND has_active_subscription");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/field/interventions", growerRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/sectors/99/interventions", new { description = "Legacy sector treatment", performedBy = "Worker", executionDate = now })).StatusCode);

        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.user_projections WHERE user_id = 78 AND NOT has_active_subscription"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM field.agronomist_affiliation_projections WHERE agronomist_id = 78 AND plantation_id = 11 AND active"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(78, "Administrator"));
        var adminVisit = new { plantationId = 11, agronomistId = 78, scheduledDate = DateOnly.FromDateTime(now.AddDays(1)), objectives = "Inspect" };
        var adminVisitResponse = await client.PostAsJsonAsync("/api/v1/field/visits", adminVisit);
        Assert.Equal(HttpStatusCode.Created, adminVisitResponse.StatusCode);
        var adminVisitId = (await adminVisitResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/field/visits/{adminVisitId}/start", new { agronomistId = 78 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/v1/field/visits/{adminVisitId}/inspections", new { clientReference = Guid.NewGuid(), sectorId = 99, agronomistId = 78, observations = Array.Empty<object>(), inspectedAt = now, linkedAlertIds = Array.Empty<long>() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/field/visits/{adminVisitId}/complete", new { agronomistId = 78 })).StatusCode);
        var adminRequest = new { plantationId = 11, sectorId = 99, registeredBy = 78, performedBy = "Worker", type = "Other", description = "Treatment", executionDate = now };
        var adminIntervention = await client.PostAsJsonAsync("/api/v1/field/interventions", adminRequest);
        Assert.Equal(HttpStatusCode.Created, adminIntervention.StatusCode);
        var adminInterventionId = (await adminIntervention.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/field/interventions/{adminInterventionId}/verify", new { agronomistId = 78, notes = "Verified" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/field/interventions", request)).StatusCode);

        var recommendationId = Guid.NewGuid();
        var recommendationBody = JsonSerializer.SerializeToUtf8Bytes(new
        {
            recommendationId = 88L,
            agronomistId = 77L,
            sectorId = 99L,
            content = "Increase irrigation frequency",
            type = "SectorSpecific",
            version = 1,
            occurredAtUtc = DateTime.UtcNow
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await channel.BasicPublishAsync("smartpalm.events", "agronomy.recommendation-published.v1", false,
                new BasicProperties { Persistent = true, MessageId = recommendationId.ToString(), Type = "agronomy.recommendation-published.v1" }, recommendationBody);
            await Task.Delay(300);
            if (await ScalarAsync("SELECT COUNT(*) FROM field.recommendation_projections WHERE recommendation_id = 88 AND published" ) == 1) break;
        }
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.recommendation_projections WHERE recommendation_id = 88 AND published"));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await channel.BasicPublishAsync("smartpalm.events", "agronomy.recommendation-published.v1", false,
                new BasicProperties { Persistent = true, MessageId = recommendationId.ToString(), Type = "agronomy.recommendation-published.v1" }, recommendationBody);
            await Task.Delay(200);
        }
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.inbox_messages WHERE event_type = 'agronomy.recommendation-published.v1'"));
    }
    [DockerRequiredFact, Trait("Category", "Integration")]
    public async Task ReportDraft_RejectsNonAgronomistsWithoutWrites()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var beforeVisits = await ScalarAsync("SELECT COUNT(*) FROM field.field_visits");
        var beforeInspections = await ScalarAsync("SELECT COUNT(*) FROM field.field_inspections");
        var beforeOutbox = await ScalarAsync("SELECT COUNT(*) FROM field.outbox_messages");
        foreach (var role in new[] { "Administrator", "PalmGrower" })
        {
            var jwt = new JwtSecurityToken("tests", "tests", [new Claim("sub", "77"), new Claim("role", role)],
                expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes("field-tests-signing-key-with-at-least-32-characters")), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            using var response = await client.PostAsync("/api/v1/field/visits/123/report-draft", null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(beforeVisits, await ScalarAsync("SELECT COUNT(*) FROM field.field_visits"));
        Assert.Equal(beforeInspections, await ScalarAsync("SELECT COUNT(*) FROM field.field_inspections"));
        Assert.Equal(beforeOutbox, await ScalarAsync("SELECT COUNT(*) FROM field.outbox_messages"));
    }
    [DockerRequiredFact, Trait("Category", "Integration")]
    public async Task ReportDraft_UsesOnlyTheAuthorizedVisitAndDoesNotWrite()
    {
        var now = DateTime.UtcNow;
        using var upstream = new DraftUpstream();
        using var telemetry = new MoistureUpstream(now);
        using var factory = CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MINIMAX_API_KEY", "synthetic-key");
            builder.UseSetting("Ingestion:BaseUrl", "http://ingestion.test");
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient(MiniMaxReportDraftProvider.ClientName).ConfigurePrimaryHttpMessageHandler(() => upstream);
                services.AddHttpClient(IngestionSoilMoistureReader.ClientName).ConfigurePrimaryHttpMessageHandler(() => telemetry);
            });
        });
        using var client = factory.CreateClient();
        await using var connection = await new ConnectionFactory { Uri = new Uri(_rabbit.GetConnectionString()) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync("smartpalm.events", ExchangeType.Topic, true, false);
        await Publish("smartpalm.identity.user-created.v1", new { userId = 77, role = "Agronomist", occurredAtUtc = now }, "field.user_projections WHERE user_id = 77");
        await Publish("smartpalm.identity.user-created.v1", new { userId = 78, role = "Agronomist", occurredAtUtc = now }, "field.user_projections WHERE user_id = 78");
        await Publish("crop.plantation-created.v1", new { plantationId = 11, palmGrowerId = 12, name = "Farm", hectares = 10, occurredAtUtc = now }, "field.plantation_projections WHERE plantation_id = 11");
        await Publish("crop.sector-assigned.v1", new { sectorId = 99, plantationId = 11, name = "Sector A", occurredAtUtc = now }, "field.sector_projections WHERE sector_id = 99");
        await Publish("crop.sector-assigned.v1", new { sectorId = 100, plantationId = 11, name = "Sector B", occurredAtUtc = now }, "field.sector_projections WHERE sector_id = 100");
        await Publish("crop.agronomist-affiliated.v1", new { affiliationId = 33, agronomistId = 77, plantationId = 11, occurredAtUtc = now }, "field.agronomist_affiliation_projections WHERE agronomist_id = 77 AND active");
        await Publish("crop.agronomist-affiliated.v1", new { affiliationId = 34, agronomistId = 78, plantationId = 11, occurredAtUtc = now }, "field.agronomist_affiliation_projections WHERE agronomist_id = 78 AND active");

        var visits = "/api/v1/field/visits";
        UseActor(77);
        var visit = await Plan();
        var path = $"{visits}/{visit}/report-draft";
        using (var response = await client.PostAsync(path, null))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("The visit must be completed.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        }
        await Record(visit, [(99, "OBS-IN-VISIT-A"), (100, "OBS-IN-VISIT-B")]);
        await Record(await Plan(), [(99, "OBS-OTHER-VISIT")]);
        var empty = await Plan();
        await Record(empty, [(99, null)]);
        var snapshot = await client.GetStringAsync($"{visits}/{visit}");
        var counts = await Counts();

        UseActor(78);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(path, null)).StatusCode);
        UseActor(77);
        await using (var db = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await db.OpenAsync();
            await using var revoke = new NpgsqlCommand("UPDATE field.agronomist_affiliation_projections SET active = false WHERE agronomist_id = 77", db);
            Assert.Equal(1, await revoke.ExecuteNonQueryAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(path, null)).StatusCode);
            await using var restore = new NpgsqlCommand("UPDATE field.agronomist_affiliation_projections SET active = true WHERE agronomist_id = 77", db);
            Assert.Equal(1, await restore.ExecuteNonQueryAsync());
        }
        using (var response = await client.PostAsync($"{visits}/{empty}/report-draft", null))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("The visit has no usable observations.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        }
        Assert.Equal(0, upstream.Calls);

        using var success = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.True(success.Headers.CacheControl?.NoStore);
        var result = await success.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(visit, result.GetProperty("visit").GetProperty("id").GetInt64());
        Assert.Equal("Completed", result.GetProperty("visit").GetProperty("status").GetString());
        Assert.Equal(new long[] { 99, 100 }, result.GetProperty("findings").EnumerateArray().Select(x => x.GetProperty("sectorId").GetInt64()).Order());
        Assert.Equal(new[] { "OBS-IN-VISIT-A", "OBS-IN-VISIT-B" }, result.GetProperty("sources").EnumerateArray().Select(x => x.GetProperty("description").GetString()).Order());
        Assert.DoesNotContain("OBS-OTHER-VISIT", upstream.Prompt);
        Assert.DoesNotContain("private-test-photo", upstream.Prompt);
        Assert.DoesNotContain("private-test-objective", upstream.Prompt);

        foreach (var invalid in new[] { "unknown", "wrong-sector", "invalid-json", "oversized", "unavailable" })
        {
            upstream.ResponseMode = invalid;
            using var response = await client.PostAsync(path, null);
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal("The report draft provider did not return a valid draft.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        }
        upstream.ResponseMode = "timeout";
        using (var response = await client.PostAsync(path, null))
        {
            Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
            Assert.Equal("The report draft provider timed out.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        }
        Assert.Equal(7, upstream.Calls);
        Assert.Equal(snapshot, await client.GetStringAsync($"{visits}/{visit}"));
        Assert.Equal(counts, await Counts());
        Assert.Empty(telemetry.Paths); // Non-irrigation, ineligible and unauthorized requests must not look up readings.

        var irrigationVisit = await Plan();
        await Record(irrigationVisit, [(99, "Área seca junto a una línea de riego.")], "Irrigation");
        var irrigationPath = $"{visits}/{irrigationVisit}/report-draft";
        var irrigationSnapshot = await client.GetStringAsync($"{visits}/{irrigationVisit}");
        var irrigationCounts = await Counts();
        UseActor(78);
        using (var denied = await client.PostAsync(irrigationPath, null))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(string.Empty, await denied.Content.ReadAsStringAsync());
        }
        Assert.Empty(telemetry.Paths);
        Assert.Equal(7, upstream.Calls);
        UseActor(77);
        upstream.ResponseMode = "speculative";

        foreach (var (mode, status, after, delta) in new (string Mode, string Status, decimal? After, decimal? Delta)[]
        {
            ("decline", "Compared", 19, -11), ("same", "Compared", 30, 0), ("increase", "Compared", 33, 3),
            ("empty", "InsufficientData", null, null), ("split", "InsufficientData", null, null),
            ("unavailable", "Unavailable", null, null)
        })
        {
            telemetry.Mode = mode;
            using var response = await client.PostAsync(irrigationPath, null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("UNSUPPORTED-MODEL-CAUSE", body);
            Assert.DoesNotContain("device-alpha", body);
            Assert.DoesNotContain("device-alpha", upstream.Prompt);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var context = Assert.Single(root.GetProperty("soilMoistureContext").EnumerateArray());
            Assert.Equal(99, context.GetProperty("sectorId").GetInt64());
            Assert.Equal("Ingestion", context.GetProperty("source").GetString());
            Assert.Equal("Unverified", context.GetProperty("origin").GetString());
            Assert.Equal(status, context.GetProperty("status").GetString());
            Assert.Equal(root.GetProperty("sources")[0].GetProperty("observationId"), context.GetProperty("referenceObservationId"), JsonElement.DeepEquals);
            Assert.Equal(now.AddHours(-2).Ticks / 10, context.GetProperty("windowFromUtc").GetDateTimeOffset().UtcTicks / 10);
            Assert.Equal(now.AddHours(2).Ticks / 10, context.GetProperty("windowToUtc").GetDateTimeOffset().UtcTicks / 10);
            Assert.StartsWith("Sector 99:", root.GetProperty("interpretation")[0].GetString());
            if (after is { } median && delta is { } difference)
            {
                Assert.Equal(3, context.GetProperty("before").GetProperty("sampleCount").GetInt32());
                Assert.Equal(30, context.GetProperty("before").GetProperty("medianPercent").GetDecimal());
                Assert.Equal(3, context.GetProperty("after").GetProperty("sampleCount").GetInt32());
                Assert.Equal(median, context.GetProperty("after").GetProperty("medianPercent").GetDecimal());
                Assert.Equal(difference, context.GetProperty("deltaPercentagePoints").GetDecimal());
                Assert.Contains(difference < 0 ? "otros puntos" : "punto cercano", root.GetProperty("outstandingChecks")[0].GetString());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, context.GetProperty("before").ValueKind);
                Assert.Equal(JsonValueKind.Null, context.GetProperty("after").ValueKind);
                Assert.Equal(JsonValueKind.Null, context.GetProperty("deltaPercentagePoints").ValueKind);
                Assert.Contains("mediciones", root.GetProperty("outstandingChecks")[0].GetString());
            }
        }
        Assert.Equal(6, telemetry.Paths.Count);
        Assert.All(telemetry.Paths, path => Assert.Equal("/api/v1/sectors/99/sensor-readings", path));
        Assert.Equal(13, upstream.Calls);
        Assert.Equal(irrigationSnapshot, await client.GetStringAsync($"{visits}/{irrigationVisit}"));
        Assert.Equal(irrigationCounts, await Counts());

        void UseActor(long id)
        {
            var jwt = new JwtSecurityToken("tests", "tests", [new Claim("sub", id.ToString()), new Claim("role", "Agronomist")],
                expires: now.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("field-tests-signing-key-with-at-least-32-characters")), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }

        async Task<long> Plan()
        {
            using var response = await client.PostAsJsonAsync(visits, new { plantationId = 11, agronomistId = 77, scheduledDate = DateOnly.FromDateTime(now.AddDays(1)), objectives = "private-test-objective" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        }

        async Task Record(long id, (long Sector, string? Observation)[] rows, string category = "General")
        {
            var endpoint = $"{visits}/{id}";
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"{endpoint}/start", new { agronomistId = 77 })).StatusCode);
            foreach (var (sector, observation) in rows)
            {
                var observations = observation is null ? Array.Empty<object>() : new object[]
                    { new { description = observation, category, severity = "Low", photoReferences = new[] { "private-test-photo" }, observedAt = now } };
                Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"{endpoint}/inspections", new { clientReference = Guid.NewGuid(), sectorId = sector, agronomistId = 77, observations, inspectedAt = now })).StatusCode);
            }
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"{endpoint}/complete", new { agronomistId = 77 })).StatusCode);
        }

        async Task Publish(string type, object payload, string rows)
        {
            var eventId = Guid.NewGuid();
            var body = JsonSerializer.SerializeToUtf8Bytes(new { eventId, eventType = type, payload });
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await channel.BasicPublishAsync("smartpalm.events", type, false, new BasicProperties { Persistent = true, MessageId = eventId.ToString(), Type = type }, body);
                await Task.Delay(150);
                if (await ScalarAsync($"SELECT COUNT(*) FROM {rows}") == 1) return;
            }
            Assert.Fail($"Event {type} was not projected.");
        }

        Task<long[]> Counts() => Task.WhenAll(new[] { "field_visits", "field_inspections", "field_observations", "outbox_messages" }
            .Select(table => ScalarAsync($"SELECT COUNT(*) FROM field.{table}")));
    }

    private sealed class DraftUpstream : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Prompt { get; private set; } = "";
        public string ResponseMode { get; set; } = "valid";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            if (ResponseMode == "oversized") return new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[128 * 1024 + 1]) };
            if (ResponseMode == "unavailable") return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("upstream-internal-detail") };
            if (ResponseMode == "timeout") throw new TaskCanceledException("upstream-internal-timeout-detail");
            using var parsed = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Prompt = parsed.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
            using var observations = JsonDocument.Parse(Prompt);
            var findings = observations.RootElement.EnumerateArray().Select(source => new
            {
                sectorId = ResponseMode == "wrong-sector" ? 999 : source.GetProperty("sectorId").GetInt64(),
                text = source.GetProperty("description").GetString(),
                observationIds = new[] { ResponseMode == "unknown" ? 999L : source.GetProperty("observationId").GetInt64() }
            }).ToArray();
            var content = ResponseMode == "invalid-json" ? "not JSON" : JsonSerializer.Serialize(new
            {
                title = "Informe de prueba", findings,
                interpretation = ResponseMode == "speculative" ? new[] { "UNSUPPORTED-MODEL-CAUSE" } : Array.Empty<string>(),
                outstandingChecks = ResponseMode == "speculative" ? new[] { "UNSUPPORTED-MODEL-CAUSE" } : Array.Empty<string>()
            });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { choices = new[] { new { finish_reason = "stop", message = new { content } } } }) };
        }
    }

    private sealed class MoistureUpstream(DateTime anchor) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public string Mode { get; set; } = "decline";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (Mode == "unavailable") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var rows = new List<object>();
            if (Mode != "empty")
            {
                rows.Add(Row(-60, 29)); rows.Add(Row(-45, 30)); rows.Add(Row(-30, 31));
                var median = Mode == "same" ? 30 : Mode == "increase" ? 33 : 19;
                var afterDevice = Mode == "split" ? "device-beta" : "device-alpha";
                rows.Add(Row(10, median - 1, afterDevice)); rows.Add(Row(30, median, afterDevice)); rows.Add(Row(60, median + 1, afterDevice));
                rows.Add(Row(-60, 29)); // Duplicate timestamp must not weight the median.
                rows.Add(Row(20, 100, sector: 100)); rows.Add(Row(25, 100, plantation: 12));
                rows.Add(Row(35, 100, unit: "Celsius")); rows.Add(Row(150, 100));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(rows) });
        }

        private object Row(int minutes, int value, string device = "device-alpha", long sector = 99,
            long plantation = 11, string unit = "Percent") => new
        {
            edgeDeviceMacAddress = "edge-alpha", iotDeviceMacAddress = device, sensorType = "SoilMoisture",
            value, unit, measuredAt = anchor.AddMinutes(minutes), plantationId = plantation, sectorId = sector
        };
    }

    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.UseSetting("ConnectionStrings:FieldDatabase", _postgres.GetConnectionString()); builder.UseSetting("Jwt:Issuer", "tests"); builder.UseSetting("Jwt:Audience", "tests"); builder.UseSetting("Jwt:Key", "field-tests-signing-key-with-at-least-32-characters"); builder.UseSetting("RabbitMq:Enabled", "true"); builder.UseSetting("RabbitMq:Uri", _rabbit.GetConnectionString()); builder.UseSetting("RabbitMq:Queue", $"smartpalm.field.tests.{Guid.NewGuid():N}"); });
    private async Task<long> ScalarAsync(string sql) { await using var connection = new NpgsqlConnection(_postgres.GetConnectionString()); await connection.OpenAsync(); await using var command = new NpgsqlCommand(sql, connection); return Convert.ToInt64(await command.ExecuteScalarAsync()); }
}
