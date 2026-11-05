using SmartPalm.FieldService.Domain.Model.ValueObjects;

namespace SmartPalm.FieldService.Domain.Model.Queries;

public record GetVisitsByAgronomistQuery(long AgronomistId, long? PlantationId, VisitStatus? Status, DateOnly? From, DateOnly? To);
