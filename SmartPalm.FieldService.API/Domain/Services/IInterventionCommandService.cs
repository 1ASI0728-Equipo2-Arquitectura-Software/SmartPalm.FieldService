using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;

namespace SmartPalm.FieldService.Domain.Services;

public interface IInterventionCommandService { Task<AgronomicIntervention> HandleAsync(RegisterInterventionCommand command, CancellationToken token); Task<AgronomicIntervention> HandleAsync(VerifyInterventionCommand command, CancellationToken token); Task<AgronomicIntervention> HandleAsync(RejectInterventionCommand command, CancellationToken token); }
