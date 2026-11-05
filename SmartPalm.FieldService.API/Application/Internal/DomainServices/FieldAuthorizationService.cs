using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
namespace SmartPalm.FieldService.Application.DomainServices;
public class FieldAuthorizationService(IFieldProjectionRepository projections, FieldAccessPolicy policy)
{
    public async Task EnsureAgronomistAccessAsync(long userId, long plantationId, CancellationToken token)
    {
        var user = await projections.FindUserAsync(userId, token); policy.EnsureAgronomist(user);
        var plantation = await projections.FindPlantationAsync(plantationId, token); var affiliated = await projections.HasAffiliationAsync(userId, plantationId, token);
        policy.EnsurePlantationAccess(plantation, affiliated || string.Equals(user?.Role, "Administrator", StringComparison.OrdinalIgnoreCase));
    }
    public async Task EnsureFieldUserAccessAsync(long userId, long plantationId, CancellationToken token)
    {
        var user = await projections.FindUserAsync(userId, token); policy.EnsureActiveFieldUser(user);
        var plantation = await projections.FindPlantationAsync(plantationId, token);
        var owns = plantation?.PalmGrowerId == userId; var affiliated = await projections.HasAffiliationAsync(userId, plantationId, token);
        var admin = string.Equals(user?.Role, "Administrator", StringComparison.OrdinalIgnoreCase); policy.EnsurePlantationAccess(plantation, owns || affiliated || admin);
    }
}
