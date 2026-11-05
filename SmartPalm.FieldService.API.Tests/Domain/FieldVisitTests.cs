using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
namespace SmartPalm.FieldService.API.Tests.Domain;
public class FieldVisitTests
{
    [Fact] public void Complete_RequiresInspection() { var visit = new FieldVisit(1, 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "Inspect palms"); visit.Start(); Assert.Throws<InvalidOperationException>(() => visit.Complete(false)); }
    [Fact] public void Lifecycle_CompletesWithInspection() { var visit = new FieldVisit(1, 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "Inspect palms"); visit.Start(); visit.Complete(true); Assert.Equal(VisitStatus.Completed, visit.Status); }
}
