using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Interfaces.REST.Controllers;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class ReportDraftTests
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("PalmGrower")]
    public async Task ReportDraft_RejectsOtherRolesBeforeAccessingData(string role)
    {
        var controller = new ReportDraftController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", "77"), new Claim("role", role)], "test"))
                }
            }
        };
        Assert.IsType<ForbidResult>(await controller.GenerateReportDraft(123L, CancellationToken.None));
    }
}
