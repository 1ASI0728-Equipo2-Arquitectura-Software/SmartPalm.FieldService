using Microsoft.EntityFrameworkCore;
using SmartPalm.FieldService.Application.CommandServices;
using SmartPalm.FieldService.Application.DomainServices;
using SmartPalm.FieldService.Application.EventHandlers;
using SmartPalm.FieldService.Application.Ports;
using SmartPalm.FieldService.Application.QueryServices;
using SmartPalm.FieldService.Domain.Repositories;
using SmartPalm.FieldService.Domain.Services;
using SmartPalm.FieldService.Infrastructure.Configuration;
using SmartPalm.FieldService.Infrastructure.Messaging;
using SmartPalm.FieldService.Infrastructure.Persistence;
namespace SmartPalm.FieldService.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddFieldInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("FieldDatabase") ?? throw new InvalidOperationException("ConnectionStrings:FieldDatabase is required.");
        services.AddDbContext<FieldDbContext>(o => o.UseNpgsql(connection).UseSnakeCaseNamingConvention()); services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddScoped<IFieldVisitRepository, FieldVisitRepository>(); services.AddScoped<IFieldInspectionRepository, FieldInspectionRepository>(); services.AddScoped<IInterventionRepository, InterventionRepository>(); services.AddScoped<IFieldProjectionRepository, FieldProjectionRepository>(); services.AddScoped<IUnitOfWork>(p => p.GetRequiredService<FieldDbContext>());
        services.AddScoped<IIntegrationEventWriter, IntegrationEventWriter>(); services.AddScoped<IInbox, Inbox>(); services.AddScoped<IntegrationEventHandlers>();
        services.AddScoped<FieldAuthorizationService>(); services.AddSingleton<FieldAccessPolicy>(); services.AddSingleton<InterventionTraceabilityService>();
        services.AddScoped<IFieldVisitCommandService, FieldVisitCommandService>(); services.AddScoped<IFieldVisitQueryService, FieldVisitQueryService>(); services.AddScoped<IFieldInspectionCommandService, FieldInspectionCommandService>(); services.AddScoped<IFieldInspectionQueryService, FieldInspectionQueryService>(); services.AddScoped<IInterventionCommandService, InterventionCommandService>(); services.AddScoped<IInterventionQueryService, InterventionQueryService>();
        services.AddHostedService<IntegrationEventConsumer>(); services.AddHostedService<OutboxPublisher>(); return services;
    }
}
