using SmartPalm.FieldService.Application.CommandServices;
using SmartPalm.FieldService.Application.DomainServices;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Commands;
using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Model.ValueObjects;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;

namespace SmartPalm.FieldService.API.Tests.Domain;

public class FieldVisitCommandServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherAffiliatedAgronomist_CannotChangeVisit(bool complete)
    {
        var fixture = new Fixture("Agronomist", complete);
        var originalStatus = fixture.Visit.Status;
        var originalDate = fixture.Visit.ActualDate;
        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Change(complete, 2));
        Assert.Equal("Visit belongs to another agronomist.", error.Message);
        Assert.Equal(originalStatus, fixture.Visit.Status);
        Assert.Equal(originalDate, fixture.Visit.ActualDate);
        Assert.Null(fixture.Visit.CompletedAtUtc);
        Assert.Null(fixture.Visit.CancellationReason);
        Assert.Equal(0, fixture.Saves);
        Assert.Equal(0, fixture.Events);
        Assert.Equal(0, fixture.InspectionChecks);
    }

    [Theory]
    [InlineData(false, "Agronomist", 1)]
    [InlineData(true, "Agronomist", 1)]
    [InlineData(false, "Administrator", 2)]
    [InlineData(true, "Administrator", 2)]
    public async Task OwnerOrAdministrator_CanChangeVisit(bool complete, string role, long actor)
    {
        var fixture = new Fixture(role, complete);
        await fixture.Change(complete, actor);
        Assert.Equal(complete ? VisitStatus.Completed : VisitStatus.Cancelled, fixture.Visit.Status);
        Assert.Equal(complete ? 1 : 0, fixture.Events);
        Assert.Equal(1, fixture.Saves);
        if (complete) Assert.NotNull(fixture.Visit.CompletedAtUtc);
        else Assert.Equal("reason", fixture.Visit.CancellationReason);
    }

    [Fact]
    public async Task Owner_CanStillStartVisit()
    {
        var fixture = new Fixture("Agronomist", false);
        await fixture.Service.HandleAsync(new StartVisitCommand(1, 1), CancellationToken.None);
        Assert.Equal(VisitStatus.InProgress, fixture.Visit.Status);
        Assert.Equal(1, fixture.Saves);
    }

    private sealed class Fixture : IFieldVisitRepository, IFieldInspectionRepository, IFieldProjectionRepository, IUnitOfWork, IIntegrationEventWriter
    {
        private readonly string role;
        public FieldVisit Visit { get; } = new(10, 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "inspect");
        public int Saves { get; private set; }
        public int Events { get; private set; }
        public int InspectionChecks { get; private set; }
        public FieldVisitCommandService Service { get; }
        public Fixture(string role, bool start)
        {
            this.role = role;
            if (start) Visit.Start();
            Service = new(this, this, this, this, new FieldAuthorizationService(this, new FieldAccessPolicy()));
        }
        public Task<FieldVisit> Change(bool complete, long actor) => complete
            ? Service.HandleAsync(new CompleteVisitCommand(1, actor), CancellationToken.None)
            : Service.HandleAsync(new CancelVisitCommand(1, actor, "reason"), CancellationToken.None);
        Task<FieldVisit?> IFieldVisitRepository.FindAsync(long id, CancellationToken token) => Task.FromResult<FieldVisit?>(Visit);
        public Task<bool> AnyByVisitAsync(long visitId, CancellationToken token) { InspectionChecks++; return Task.FromResult(true); }
        public Task<UserProjection?> FindUserAsync(long id, CancellationToken token) => Task.FromResult<UserProjection?>(new UserProjection { UserId = id, Role = role });
        public Task<PlantationProjection?> FindPlantationAsync(long id, CancellationToken token) => Task.FromResult<PlantationProjection?>(new PlantationProjection { PlantationId = id, Active = true });
        public Task<bool> HasAffiliationAsync(long id, long plantationId, CancellationToken token) => Task.FromResult(true);
        public Task<int> SaveChangesAsync(CancellationToken token = default) { Saves++; return Task.FromResult(1); }
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token) => operation(token);
        public void Enqueue(string eventType, object payload) => Events++;
        public void Add(FieldVisit visit) => throw new NotSupportedException();
        public Task<IReadOnlyList<FieldVisit>> ListAsync(long? agronomistId, long? plantationId, VisitStatus? status, DateOnly? from, DateOnly? to, CancellationToken token) => throw new NotSupportedException();
        Task<FieldInspection?> IFieldInspectionRepository.FindAsync(long id, CancellationToken token) => throw new NotSupportedException();
        public Task<FieldInspection?> FindByClientReferenceAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<FieldInspection>> ListByPlantationAsync(long id, long? sector, DateTime? from, DateTime? to, CancellationToken token) => throw new NotSupportedException();
        public void Add(FieldInspection inspection) => throw new NotSupportedException();
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
