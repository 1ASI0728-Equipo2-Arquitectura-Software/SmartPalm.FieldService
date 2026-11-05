namespace SmartPalm.FieldService.Domain.Model.Entities;

public class AlertProjection { public long AlertId { get; set; } public long? PlantationId { get; set; } public long? SectorId { get; set; } public string Status { get; set; } = "Active"; public DateTime UpdatedAtUtc { get; set; } }
