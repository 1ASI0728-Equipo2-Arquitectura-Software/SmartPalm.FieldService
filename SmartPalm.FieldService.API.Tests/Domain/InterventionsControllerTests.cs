using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Interfaces.REST.Controllers;
using SmartPalm.FieldService.Interfaces.REST.Resources;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class InterventionsControllerTests
{
    [Fact]
    public async Task Register_RejectsSpoofedRegisteredByBeforeCallingCommands()
    {
        var controller = new InterventionsController(null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "77")], "test")) }
            }
        };
        var result = await controller.RegisterIntervention(new RegisterFieldInterventionResource(11, null, 88, "worker", "Other", "work", DateTime.UtcNow), CancellationToken.None);
        Assert.IsType<ForbidResult>(result);
    }
}
