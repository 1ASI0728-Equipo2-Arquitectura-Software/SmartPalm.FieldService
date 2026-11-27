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
    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.UseSetting("ConnectionStrings:FieldDatabase", _postgres.GetConnectionString()); builder.UseSetting("Jwt:Issuer", "tests"); builder.UseSetting("Jwt:Audience", "tests"); builder.UseSetting("Jwt:Key", "field-tests-signing-key-with-at-least-32-characters"); builder.UseSetting("RabbitMq:Enabled", "true"); builder.UseSetting("RabbitMq:Uri", _rabbit.GetConnectionString()); builder.UseSetting("RabbitMq:Queue", $"smartpalm.field.tests.{Guid.NewGuid():N}"); });
    private async Task<long> ScalarAsync(string sql) { await using var connection = new NpgsqlConnection(_postgres.GetConnectionString()); await connection.OpenAsync(); await using var command = new NpgsqlCommand(sql, connection); return Convert.ToInt64(await command.ExecuteScalarAsync()); }
}
