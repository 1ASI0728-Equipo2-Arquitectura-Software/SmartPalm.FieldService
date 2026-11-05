using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Queries;
using SmartPalm.FieldService.Domain.Services;
using SmartPalm.FieldService.Interfaces.REST.Resources;
using SmartPalm.FieldService.Interfaces.REST.Transform;
using Swashbuckle.AspNetCore.Annotations;
namespace SmartPalm.FieldService.Interfaces.REST.Controllers;
[Authorize, ApiController, Route("api/v1/field"), SwaggerTag("Online and offline field inspections")]
public class FieldInspectionsController(IFieldInspectionCommandService commands, IFieldInspectionQueryService queries) : ControllerBase
{
[HttpPost("visits/{visitId:long}/inspections")] public async Task<IActionResult> RegisterFieldInspection(long visitId, RegisterInspectionResource r, CancellationToken t) { var x = await commands.HandleAsync(new RegisterInspectionCommand(r.clientReference, visitId, r.sectorId, r.agronomistId, r.observations.Select(FieldAssemblers.ToInput).ToArray(), r.inspectedAt, r.linkedAlertIds, r.offline), t); return Created($"/api/v1/field/inspections/{x.Id}", FieldAssemblers.ToResource(x)); }
[HttpPost("visits/{visitId:long}/inspections/sync")] public async Task<IActionResult> SynchronizeOfflineInspections(long visitId, SyncInspectionsResource r, CancellationToken t) { var items = r.inspections.Select(x => new RegisterInspectionCommand(x.clientReference, visitId, x.sectorId, r.agronomistId, x.observations.Select(FieldAssemblers.ToInput).ToArray(), x.inspectedAt, x.linkedAlertIds, true)).ToArray(); return Ok((await commands.HandleAsync(new SyncOfflineInspectionsCommand(visitId, r.agronomistId, items), t)).Select(FieldAssemblers.ToResource)); }
[HttpGet("inspections/{id:long}")] public async Task<IActionResult> GetFieldInspectionById(long id, CancellationToken t) { var x = await queries.HandleAsync(new GetInspectionByIdQuery(id), t); return x is null ? NotFound() : Ok(FieldAssemblers.ToResource(x)); }
[HttpGet("plantations/{plantationId:long}/inspections")] public async Task<IActionResult> GetFieldInspectionsByPlantation(long plantationId, [FromQuery] long? sectorId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken t) => Ok((await queries.HandleAsync(new GetInspectionsByPlantationQuery(plantationId, sectorId, from, to), t)).Select(FieldAssemblers.ToResource));
[HttpPost("inspections/{id:long}/observations")] public async Task<IActionResult> AddInspectionObservation(long id, AddObservationResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new AddObservationCommand(id, FieldAssemblers.ToInput(r.observation)), t)));
[HttpPost("inspections/{id:long}/alerts")] public async Task<IActionResult> LinkInspectionToAlert(long id, LinkAlertResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new LinkInspectionToAlertCommand(id, r.alertId), t)));
}
