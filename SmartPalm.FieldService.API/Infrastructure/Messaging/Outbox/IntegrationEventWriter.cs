using System.Text.Json;
using System.Text.Json.Serialization;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Infrastructure.Persistence;
namespace SmartPalm.FieldService.Infrastructure.Messaging;
internal class IntegrationEventWriter(FieldDbContext db) : IIntegrationEventWriter
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    public void Enqueue(string type, object payload) => db.OutboxMessages.Add(new OutboxMessage { Id = Guid.NewGuid(), EventType = type, Content = JsonSerializer.Serialize(payload, payload.GetType(), Options), OccurredOnUtc = DateTime.UtcNow });
    private static JsonSerializerOptions CreateOptions() { var result = new JsonSerializerOptions(JsonSerializerDefaults.Web); result.Converters.Add(new JsonStringEnumConverter()); return result; }
}
