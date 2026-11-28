using System.Security.Claims;

namespace SmartPalm.FieldService.Interfaces.REST.Controllers;

internal readonly record struct FieldActor(long Id, bool IsAdmin, bool IsAgronomist, bool IsGrower)
{
    public static FieldActor From(ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(subject, out var id)) throw new UnauthorizedAccessException("JWT subject is required.");
        bool HasRole(string role) => user.IsInRole(role) || user.HasClaim(ClaimTypes.Role, role) || user.HasClaim("role", role);
        return new FieldActor(id, HasRole("Administrator"), HasRole("Agronomist"), HasRole("PalmGrower"));
    }
}
