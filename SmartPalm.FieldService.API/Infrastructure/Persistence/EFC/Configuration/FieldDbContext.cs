using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Domain.Model.Aggregates;
using SmartPalm.FieldService.Domain.Model.Entities;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Infrastructure.Messaging;
namespace SmartPalm.FieldService.Infrastructure.Persistence;
public class FieldDbContext(DbContextOptions<FieldDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<FieldVisit> Visits => Set<FieldVisit>(); public DbSet<FieldInspection> Inspections => Set<FieldInspection>(); public DbSet<FieldObservation> Observations => Set<FieldObservation>();
    public DbSet<InspectionAlertLink> InspectionAlertLinks => Set<InspectionAlertLink>(); public DbSet<AgronomicIntervention> Interventions => Set<AgronomicIntervention>();
    public DbSet<UserProjection> Users => Set<UserProjection>(); public DbSet<PlantationProjection> Plantations => Set<PlantationProjection>(); public DbSet<SectorProjection> Sectors => Set<SectorProjection>();
    public DbSet<AgronomistAffiliationProjection> Affiliations => Set<AgronomistAffiliationProjection>(); public DbSet<AlertProjection> Alerts => Set<AlertProjection>(); public DbSet<RecommendationProjection> Recommendations => Set<RecommendationProjection>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>(); public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("field");
        b.Entity<FieldVisit>(e => { e.ToTable("field_visits"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); e.Property(x => x.Objectives).HasMaxLength(1000).IsRequired(); e.Property(x => x.CancellationReason).HasMaxLength(500); e.HasIndex(x => new { x.AgronomistId, x.ScheduledDate }); e.HasIndex(x => new { x.PlantationId, x.Status }); });
        b.Entity<FieldInspection>(e => { e.ToTable("field_inspections"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); e.Property(x => x.SyncStatus).HasConversion<string>().HasMaxLength(20); e.HasIndex(x => x.ClientReference).IsUnique(); e.HasIndex(x => new { x.PlantationId, x.InspectedAtUtc }); e.HasOne<FieldVisit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Cascade); e.HasMany(x => x.Observations).WithOne().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Cascade); e.HasMany(x => x.AlertLinks).WithOne().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<FieldObservation>(e => { e.ToTable("field_observations"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); e.Property(x => x.Description).HasMaxLength(2000).IsRequired(); e.Property(x => x.Category).HasConversion<string>().HasMaxLength(30); e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20); e.Property(x => x.PhotoReferencesJson).HasColumnType("jsonb").IsRequired(); });
        b.Entity<InspectionAlertLink>(e => { e.ToTable("inspection_alert_links"); e.HasKey(x => new { x.InspectionId, x.AlertId }); });
        b.Entity<AgronomicIntervention>(e => { e.ToTable("agronomic_interventions"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); e.Property(x => x.PerformedBy).HasMaxLength(200).IsRequired(); e.Property(x => x.Description).HasMaxLength(2000).IsRequired(); e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); e.Property(x => x.VerificationNotes).HasMaxLength(2000); e.HasIndex(x => new { x.PlantationId, x.ExecutedAtUtc }); e.HasIndex(x => x.OriginRecommendationId); e.HasOne<FieldInspection>().WithMany().HasForeignKey(x => x.OriginInspectionId).OnDelete(DeleteBehavior.SetNull); });
        b.Entity<UserProjection>(e => { e.ToTable("user_projections"); e.HasKey(x => x.UserId); e.Property(x => x.Role).HasMaxLength(40).IsRequired(); });
        b.Entity<PlantationProjection>(e => { e.ToTable("plantation_projections"); e.HasKey(x => x.PlantationId); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); });
        b.Entity<SectorProjection>(e => { e.ToTable("sector_projections"); e.HasKey(x => x.SectorId); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); e.HasIndex(x => x.PlantationId); });
        b.Entity<AgronomistAffiliationProjection>(e => { e.ToTable("agronomist_affiliation_projections"); e.HasKey(x => x.AffiliationId); e.HasIndex(x => new { x.AgronomistId, x.PlantationId }).IsUnique(); });
        b.Entity<AlertProjection>(e => { e.ToTable("alert_projections"); e.HasKey(x => x.AlertId); e.Property(x => x.Status).HasMaxLength(20).IsRequired(); e.HasIndex(x => x.SectorId); });
        b.Entity<RecommendationProjection>(e => { e.ToTable("recommendation_projections"); e.HasKey(x => x.RecommendationId); e.Property(x => x.Content).HasMaxLength(500).IsRequired(); e.HasIndex(x => x.SectorId); });
        b.Entity<InboxMessage>(e => { e.ToTable("inbox_messages"); e.HasKey(x => x.Id); e.Property(x => x.EventType).HasMaxLength(200).IsRequired(); });
        b.Entity<OutboxMessage>(e => { e.ToTable("outbox_messages"); e.HasKey(x => x.Id); e.Property(x => x.EventType).HasMaxLength(200).IsRequired(); e.Property(x => x.Content).HasColumnType("jsonb").IsRequired(); e.Property(x => x.Error).HasMaxLength(2000); e.HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc }); });
    }
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token) { await using var tx = await Database.BeginTransactionAsync(token); try { var result = await operation(token); await tx.CommitAsync(token); return result; } catch { await tx.RollbackAsync(token); throw; } }
}
