namespace SmartPalm.FieldService.Domain.Model.Entities;

public class SectorProjection { public long SectorId { get; set; } public long PlantationId { get; set; } public string Name { get; set; } = string.Empty; public bool Active { get; set; } public DateTime UpdatedAtUtc { get; set; } }
