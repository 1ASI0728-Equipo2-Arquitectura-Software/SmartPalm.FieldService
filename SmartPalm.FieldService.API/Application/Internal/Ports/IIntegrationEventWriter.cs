namespace SmartPalm.FieldService.Application.Ports;

public interface IIntegrationEventWriter { void Enqueue(string eventType, object payload); }
