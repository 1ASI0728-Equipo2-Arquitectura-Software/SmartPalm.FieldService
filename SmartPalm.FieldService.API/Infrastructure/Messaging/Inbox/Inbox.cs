using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Infrastructure.Persistence;
namespace SmartPalm.FieldService.Infrastructure.Messaging;
internal class Inbox(FieldDbContext db) : IInbox
{
    public Task<bool> HasProcessedAsync(Guid id, CancellationToken token) => db.InboxMessages.AnyAsync(x => x.Id == id, token);
    public void MarkProcessed(Guid id, string type) => db.InboxMessages.Add(new InboxMessage { Id = id, EventType = type, ProcessedOnUtc = DateTime.UtcNow });
}
