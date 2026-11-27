using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Services;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class FieldAccessPolicyTests
{
    [Theory]
    [InlineData("Agronomist")]
    [InlineData("Administrator")]
    public void FieldRoles_DoNotRequireSubscription(string role)
    {
        var policy = new FieldAccessPolicy();
        var user = new UserProjection { Role = role, HasActiveSubscription = false };
        policy.EnsureActiveFieldUser(user);
        policy.EnsureAgronomist(user);
    }

    [Fact]
    public void PalmGrower_StillRequiresSubscription()
    {
        var policy = new FieldAccessPolicy();
        var user = new UserProjection { Role = "PalmGrower" };
        Assert.Throws<InvalidOperationException>(() => policy.EnsureActiveFieldUser(user));
        user.HasActiveSubscription = true;
        policy.EnsureActiveFieldUser(user);
        Assert.Throws<UnauthorizedAccessException>(() => policy.EnsureAgronomist(user));
        Assert.Throws<ArgumentException>(() => policy.EnsureActiveFieldUser(null));
    }
}
