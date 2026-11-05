namespace SmartPalm.FieldService.Domain.Model.Entities;

public class InspectionAlertLink { private InspectionAlertLink() { } public InspectionAlertLink(long alertId) => AlertId = alertId; public long InspectionId { get; private set; } public long AlertId { get; private set; } }
