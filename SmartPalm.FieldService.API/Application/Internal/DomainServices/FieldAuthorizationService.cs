using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
namespace SmartPalm.FieldService.Application.DomainServices;
public class FieldAuthorizationService(IFieldProjectionRepository projections, FieldAccessPolicy policy)
{
    public async Task<bool> CanReadPlantationAsync(long actorId, bool admin, bool agronomist, bool grower, long plantationId, CancellationToken token)
    {
        if (admin) return true;
        if (agronomist && await projections.HasAffiliationAsync(actorId, plantationId, token)) return true;
        return grower && (await projections.FindPlantationAsync(plantationId, token))?.PalmGrowerId == actorId;
    }
    public async Task EnsurePlantationReadAccessAsync(long actorId, bool admin, bool agronomist, bool grower, long plantationId, CancellationToken token)
    {
        if (!await CanReadPlantationAsync(actorId, admin, agronomist, grower, plantationId, token))
            throw new UnauthorizedAccessException("Plantation read access is required.");
    }
    public async Task EnsureAgronomistAccessAsync(long userId, long plantationId, CancellationToken token)
    {
        var user = await projections.FindUserAsync(userId, token); policy.EnsureAgronomist(user);
        var plantation = await projections.FindPlantationAsync(plantationId, token); var affiliated = await projections.HasAffiliationAsync(userId, plantationId, token);
        policy.EnsurePlantationAccess(plantation, affiliated || string.Equals(user?.Role, "Administrator", StringComparison.OrdinalIgnoreCase));
    }
    public async Task EnsureVisitAuthorAccessAsync(long userId, long authorId, long plantationId, CancellationToken token)
    {
        await EnsureAgronomistAccessAsync(userId, plantationId, token);
        if (userId != authorId)
        {
            var user = await projections.FindUserAsync(userId, token);
            if (!string.Equals(user?.Role, "Administrator", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Visit belongs to another agronomist.");
        }
    }
    public async Task EnsureFieldUserAccessAsync(long userId, long plantationId, CancellationToken token)
    {
        var user = await projections.FindUserAsync(userId, token); policy.EnsureActiveFieldUser(user);
        var plantation = await projections.FindPlantationAsync(plantationId, token);
        var owns = plantation?.PalmGrowerId == userId; var affiliated = await projections.HasAffiliationAsync(userId, plantationId, token);
        var admin = string.Equals(user?.Role, "Administrator", StringComparison.OrdinalIgnoreCase);
        var agronomist = string.Equals(user?.Role, "Agronomist", StringComparison.OrdinalIgnoreCase);
        var grower = string.Equals(user?.Role, "PalmGrower", StringComparison.OrdinalIgnoreCase);
        if (!admin && !agronomist && !grower) throw new UnauthorizedAccessException("Field user role is required.");
        policy.EnsurePlantationAccess(plantation, admin || affiliated || (grower && owns));
    }
}
