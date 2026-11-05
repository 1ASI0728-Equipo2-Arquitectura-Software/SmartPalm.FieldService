namespace SmartPalm.FieldService.Application.Ports;

public interface IInbox { Task<bool> HasProcessedAsync(Guid id, CancellationToken token); void MarkProcessed(Guid id, string type); }
