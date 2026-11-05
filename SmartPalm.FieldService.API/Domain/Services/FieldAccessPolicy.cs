using SmartPalm.FieldService.Domain.Model.Entities;

namespace SmartPalm.FieldService.Domain.Services;

public class FieldAccessPolicy
{
    public void EnsureActiveFieldUser(UserProjection? user) { if (user is null) throw new ArgumentException("User does not exist."); if (!user.HasActiveSubscription) throw new InvalidOperationException("User does not have an active subscription."); }
    public void EnsureAgronomist(UserProjection? user) { EnsureActiveFieldUser(user); if (!string.Equals(user!.Role, "Agronomist", StringComparison.OrdinalIgnoreCase) && !string.Equals(user.Role, "Administrator", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Agronomist role is required."); }
    public void EnsurePlantationAccess(PlantationProjection? plantation, bool affiliated) { if (plantation is null || !plantation.Active) throw new ArgumentException("Plantation does not exist or is inactive."); if (!affiliated) throw new UnauthorizedAccessException("Agronomist is not affiliated with the plantation."); }
}
