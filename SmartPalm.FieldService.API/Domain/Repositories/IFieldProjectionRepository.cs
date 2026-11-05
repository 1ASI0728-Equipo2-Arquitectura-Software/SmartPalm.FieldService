using SmartPalm.FieldService.Domain.Model.Entities;

namespace SmartPalm.FieldService.Domain.Repositories;

public interface IFieldProjectionRepository
{
    Task<UserProjection?> FindUserAsync(long id, CancellationToken token); Task<PlantationProjection?> FindPlantationAsync(long id, CancellationToken token); Task<SectorProjection?> FindSectorAsync(long id, CancellationToken token);
    Task<AlertProjection?> FindAlertAsync(long id, CancellationToken token); Task<RecommendationProjection?> FindRecommendationAsync(long id, CancellationToken token); Task<bool> HasAffiliationAsync(long agronomistId, long plantationId, CancellationToken token);
    Task<UserProjection> GetOrCreateUserAsync(long id, CancellationToken token); Task<PlantationProjection> GetOrCreatePlantationAsync(long id, CancellationToken token); Task<SectorProjection> GetOrCreateSectorAsync(long id, CancellationToken token);
    Task<AgronomistAffiliationProjection> GetOrCreateAffiliationAsync(long id, long agronomistId, long plantationId, CancellationToken token); Task<IReadOnlyList<AgronomistAffiliationProjection>> ListAffiliationsAsync(long agronomistId, long plantationId, CancellationToken token);
    Task<AlertProjection> GetOrCreateAlertAsync(long id, CancellationToken token); Task<RecommendationProjection> GetOrCreateRecommendationAsync(long id, CancellationToken token);
}
