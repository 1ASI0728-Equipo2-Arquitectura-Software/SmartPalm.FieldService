using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Application.QueryServices;
using Swashbuckle.AspNetCore.Annotations;

namespace SmartPalm.FieldService.Interfaces.REST.Controllers;

[Authorize, ApiController, Route("api/v1/field/visits")]
public sealed class ReportDraftController(ReportDraftService drafts) : ControllerBase
{
    [HttpPost("{visitId:long}/report-draft")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SwaggerOperation(Summary = "Generate an unsaved AI draft from a completed visit's recorded observations")]
    [ProducesResponseType<ReportDraftResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GenerateReportDraft(long visitId, CancellationToken token)
    {
        var actor = FieldActor.From(User);
        if (!actor.IsAgronomist || actor.IsAdmin) return Forbid();
        try
        {
            return Ok(await drafts.GenerateAsync(actor.Id, visitId, token));
        }
        catch (ReportDraftException error)
        {
            return error.Failure switch
            {
                ReportDraftFailure.NotFound => NotFound(),
                ReportDraftFailure.Forbidden => Forbid(),
                ReportDraftFailure.VisitNotCompleted => Problem(statusCode: 409, title: "The visit must be completed."),
                ReportDraftFailure.InsufficientEvidence => Problem(statusCode: 422, title: "The visit has no usable observations."),
                ReportDraftFailure.ProviderTimeout => Problem(statusCode: 504, title: "The report draft provider timed out."),
                _ => Problem(statusCode: 502, title: "The report draft provider did not return a valid draft.")
            };
        }
    }
}
