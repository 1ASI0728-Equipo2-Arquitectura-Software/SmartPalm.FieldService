using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Queries;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
using SmartPalm.FieldService.Interfaces.REST.Resources;
using SmartPalm.FieldService.Interfaces.REST.Transform;
using Swashbuckle.AspNetCore.Annotations;
namespace SmartPalm.FieldService.Interfaces.REST.Controllers;
[Authorize, ApiController, Route("api"), SwaggerTag("Agronomic intervention traceability")]
public class InterventionsController(IInterventionCommandService commands, IInterventionQueryService queries, IFieldProjectionRepository projections) : ControllerBase
{
    [HttpPost("v1/field/interventions")]
    public async Task<IActionResult> RegisterIntervention(RegisterFieldInterventionResource r, CancellationToken t)
    {
        var x = await commands.HandleAsync(new RegisterInterventionCommand(
            r.plantationId, 
            r.sectorId, 
            r.registeredBy, 
            r.performedBy, 
            FieldAssemblers.Parse<Domain.Model.ValueObjects.InterventionType>(r.type), 
            r.description, 
            r.originRecommendationId, 
            r.originInspectionId, 
            r.executionDate), 
            t
        ); 
        return Created($"/api/v1/field/interventions/{x.Id}", FieldAssemblers.ToResource(x));
    }

    [HttpPost("v1/sectors/{sectorId:long}/interventions")]
    public async Task<IActionResult> RegisterSectorIntervention(long sectorId, RegisterInterventionResource r,
        CancellationToken t)
    {
        var sector = await projections.FindSectorAsync(sectorId, t) 
                     ?? throw new ArgumentException("Sector does not exist in the local projection."); 
        var registeredBy = long.TryParse(User.FindFirst("sub")?.Value, out var userId) 
            ? userId : throw new UnauthorizedAccessException("JWT subject is required."); 
        var x = await commands.HandleAsync(new RegisterInterventionCommand(sector.PlantationId, 
            sectorId, registeredBy, r.performedBy, Domain.Model.ValueObjects.InterventionType.Other, r.description, r.originRecommendationId, null, r.executionDate), t); 
        return Created($"/api/v1/sectors/{sectorId}/interventions/{x.Id}", FieldAssemblers.ToLegacyResource(x));
    }

    [HttpGet("v1/field/interventions/{id:long}")]
    public async Task<IActionResult> GetAgronomicInterventionById(long id, CancellationToken t)
    {
        var x = await queries.HandleAsync(new GetInterventionByIdQuery(id), t); 
        return x is null ? NotFound() : Ok(FieldAssemblers.ToResource(x));
    }

    [HttpGet("v1/interventions/{id:long}")]
    public async Task<IActionResult> GetLegacyAgronomicInterventionById(long id, CancellationToken t)
    {
        var x = await queries.HandleAsync(new GetInterventionByIdQuery(id), t); 
        return x is null ? NotFound() : Ok(FieldAssemblers.ToLegacyResource(x));
    }
    [HttpGet("v1/field/plantations/{plantationId:long}/interventions")] 
    public async Task<IActionResult> ListInterventionsByPlantation(long plantationId, [FromQuery] long? sectorId, [FromQuery] string? type, [FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken t) => Ok((await queries.HandleAsync(new GetInterventionsByPlantationQuery(plantationId, sectorId, FieldAssemblers.ParseInterventionType(type), FieldAssemblers.ParseInterventionStatus(status), from, to), t)).Select(FieldAssemblers.ToResource));
[HttpGet("v1/plantations/{plantationId:long}/interventions")] public async Task<IActionResult> ListLegacyInterventionsByPlantation(long plantationId, CancellationToken t) => Ok((await queries.HandleAsync(new GetInterventionsByPlantationQuery(plantationId, null, null, null, null, null), t)).Select(FieldAssemblers.ToLegacyResource));
[HttpGet("v1/sectors/{sectorId:long}/interventions")] public async Task<IActionResult> ListInterventionsBySector(long sectorId, CancellationToken t) => Ok((await queries.HandleAsync(new GetInterventionsBySectorQuery(sectorId), t)).Select(FieldAssemblers.ToLegacyResource));
[HttpGet("v1/recommendations/{recommendationId:long}/interventions")] public async Task<IActionResult> ListInterventionsByRecommendation(long recommendationId, CancellationToken t) => Ok((await queries.HandleAsync(new GetInterventionsByRecommendationQuery(recommendationId), t)).Select(FieldAssemblers.ToLegacyResource));
[HttpPatch("v1/field/interventions/{id:long}/verify")] public async Task<IActionResult> VerifyIntervention(long id, ReviewInterventionResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new VerifyInterventionCommand(id, r.agronomistId, r.notes), t)));
[HttpPatch("v1/field/interventions/{id:long}/reject")] public async Task<IActionResult> RejectIntervention(long id, ReviewInterventionResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new RejectInterventionCommand(id, r.agronomistId, r.notes), t)));
}
