using SmartPalm.FieldService.Application.DomainServices;
using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class FieldAuthorizationServiceTests
{
    [Theory]
    [InlineData("Administrator", false)]
    [InlineData("Agronomist", true)]
    public async Task AuthorizedFieldRoles_DoNotNeedSubscription(string role, bool affiliated)
    {
        var repository = new ProjectionStub { User = new UserProjection { Role = role }, Affiliated = affiliated };
        var service = new FieldAuthorizationService(repository, new FieldAccessPolicy());
        await service.EnsureAgronomistAccessAsync(1, 10, CancellationToken.None);
        await service.EnsureFieldUserAccessAsync(1, 10, CancellationToken.None);
    }

    [Fact]
    public async Task UnaffiliatedAdministrator_CanRegisterIntervention()
    {
        var service = new FieldAuthorizationService(new ProjectionStub { User = new UserProjection { Role = "Administrator" } }, new FieldAccessPolicy());
        await service.EnsureFieldUserAccessAsync(1, 10, CancellationToken.None);
    }

    [Fact]
    public async Task UnaffiliatedAgronomist_IsRejected()
    {
        var service = new FieldAuthorizationService(new ProjectionStub { User = new UserProjection { Role = "Agronomist" } }, new FieldAccessPolicy());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureAgronomistAccessAsync(1, 10, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureFieldUserAccessAsync(1, 10, CancellationToken.None));
    }

    [Fact]
    public async Task GrowerOwner_MustHaveSubscription()
    {
        var user = new UserProjection { Role = "PalmGrower" };
        var service = new FieldAuthorizationService(new ProjectionStub { User = user }, new FieldAccessPolicy());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureFieldUserAccessAsync(1, 10, CancellationToken.None));
        user.HasActiveSubscription = true;
        await service.EnsureFieldUserAccessAsync(1, 10, CancellationToken.None);
    }

    private sealed class ProjectionStub : IFieldProjectionRepository
    {
        public UserProjection? User { get; init; }
        public bool Affiliated { get; init; }
        public Task<UserProjection?> FindUserAsync(long id, CancellationToken token) => Task.FromResult(User);
        public Task<PlantationProjection?> FindPlantationAsync(long id, CancellationToken token) => Task.FromResult<PlantationProjection?>(new PlantationProjection { PlantationId = 10, PalmGrowerId = 1, Active = true });
        public Task<bool> HasAffiliationAsync(long id, long plantationId, CancellationToken token) => Task.FromResult(Affiliated);
        public Task<SectorProjection?> FindSectorAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<AlertProjection?> FindAlertAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<RecommendationProjection?> FindRecommendationAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<UserProjection> GetOrCreateUserAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<PlantationProjection> GetOrCreatePlantationAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<SectorProjection> GetOrCreateSectorAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<AgronomistAffiliationProjection> GetOrCreateAffiliationAsync(long id, long agronomistId, long plantationId, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<AgronomistAffiliationProjection>> ListAffiliationsAsync(long agronomistId, long plantationId, CancellationToken token) => throw new NotSupportedException();
        public Task<AlertProjection> GetOrCreateAlertAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<RecommendationProjection> GetOrCreateRecommendationAsync(long id, CancellationToken token) => throw new NotSupportedException();
    }
}
