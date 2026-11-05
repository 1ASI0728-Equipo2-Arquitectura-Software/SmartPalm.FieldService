using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Queries;
using SmartPalm.FieldService.Domain.Services;
using SmartPalm.FieldService.Interfaces.REST.Resources;
using SmartPalm.FieldService.Interfaces.REST.Transform;
using Swashbuckle.AspNetCore.Annotations;
namespace SmartPalm.FieldService.Interfaces.REST.Controllers;
[Authorize, ApiController, Route("api/v1/field/visits"), SwaggerTag("Field visit planning and lifecycle")]
public class FieldVisitsController(IFieldVisitCommandService commands, IFieldVisitQueryService queries) : ControllerBase
{
[HttpPost] public async Task<IActionResult> PlanFieldVisit(PlanVisitResource r, CancellationToken t) { var x = await commands.HandleAsync(new PlanVisitCommand(r.plantationId, r.agronomistId, r.scheduledDate, r.objectives), t); return Created($"/api/v1/field/visits/{x.Id}", FieldAssemblers.ToResource(x)); }
[HttpGet] public async Task<IActionResult> GetFieldVisitsByAgronomist([FromQuery] long agronomistId, [FromQuery] long? plantationId, [FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken t) => Ok((await queries.HandleAsync(new GetVisitsByAgronomistQuery(agronomistId, plantationId, FieldAssemblers.ParseVisitStatus(status), from, to), t)).Select(FieldAssemblers.ToResource));
[HttpGet("{id:long}")] public async Task<IActionResult> GetFieldVisitById(long id, CancellationToken t) { var x = await queries.HandleAsync(new GetVisitByIdQuery(id), t); return x is null ? NotFound() : Ok(FieldAssemblers.ToResource(x)); }
[HttpPatch("{id:long}/start")] public async Task<IActionResult> StartFieldVisit(long id, VisitActionResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new StartVisitCommand(id, r.agronomistId), t)));
[HttpPatch("{id:long}/complete")] public async Task<IActionResult> CompleteFieldVisit(long id, VisitActionResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new CompleteVisitCommand(id, r.agronomistId), t)));
[HttpPatch("{id:long}/cancel")] public async Task<IActionResult> CancelFieldVisit(long id, CancelVisitResource r, CancellationToken t) => Ok(FieldAssemblers.ToResource(await commands.HandleAsync(new CancelVisitCommand(id, r.agronomistId, r.reason), t)));
}
