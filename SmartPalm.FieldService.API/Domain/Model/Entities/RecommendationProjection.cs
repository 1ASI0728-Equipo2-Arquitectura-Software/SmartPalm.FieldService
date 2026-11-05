namespace SmartPalm.FieldService.Domain.Model.Entities;

public class RecommendationProjection { public long RecommendationId { get; set; } public long AgronomistId { get; set; } public long? SectorId { get; set; } public string Content { get; set; } = string.Empty; public bool Published { get; set; } public DateTime UpdatedAtUtc { get; set; } }
