using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Interfaces.REST.Controllers;
using SmartPalm.FieldService.Interfaces.REST.Resources;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class WriteIdentityTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("cancel")]
    [InlineData("inspection")]
    [InlineData("sync")]
    [InlineData("verify")]
    [InlineData("reject")]
    public async Task SpoofedAgronomist_IsForbiddenBeforeDependencies(string action)
    {
        var context = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "77")], "test")) } };
        var visits = new FieldVisitsController(null!, null!) { ControllerContext = context };
        var inspections = new FieldInspectionsController(null!, null!) { ControllerContext = context };
        var interventions = new InterventionsController(null!, null!, null!) { ControllerContext = context };
        var result = action switch
        {
            "create" => await visits.PlanFieldVisit(new PlanVisitResource(1, 88, DateOnly.FromDateTime(DateTime.UtcNow), "work"), default),
            "start" => await visits.StartFieldVisit(1, new VisitActionResource(88), default),
            "complete" => await visits.CompleteFieldVisit(1, new VisitActionResource(88), default),
            "cancel" => await visits.CancelFieldVisit(1, new CancelVisitResource(88, "reason"), default),
            "inspection" => await inspections.RegisterFieldInspection(1, new RegisterInspectionResource(Guid.NewGuid(), 1, 88, [], DateTime.UtcNow, null), default),
            "sync" => await inspections.SynchronizeOfflineInspections(1, new SyncInspectionsResource(88, []), default),
            "verify" => await interventions.VerifyIntervention(1, new ReviewInterventionResource(88, "notes"), default),
            _ => await interventions.RejectIntervention(1, new ReviewInterventionResource(88, "notes"), default)
        };
        Assert.IsType<ForbidResult>(result);
    }
}
