using System.Net;
using System.Text.Json;
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
        var id = Guid.NewGuid(); var body = JsonSerializer.SerializeToUtf8Bytes(new { userId = 77, username = "field-user", email = "field@test.local", fullName = "Field User", role = "Agronomist", occurredAtUtc = DateTime.UtcNow }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        for (var attempt = 0; attempt < 20; attempt++) { await channel.BasicPublishAsync("smartpalm.events", "smartpalm.identity.user-created.v1", false, new BasicProperties { Persistent = true, MessageId = id.ToString(), Type = "smartpalm.identity.user-created.v1" }, body); await Task.Delay(300); if (await ScalarAsync("SELECT COUNT(*) FROM field.inbox_messages") == 1) break; }
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.user_projections WHERE user_id = 77")); Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM field.inbox_messages"));

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
