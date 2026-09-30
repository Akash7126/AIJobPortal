using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Application.Interfaces;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using JobPlatform.EmployerOnboarding.Infrastructure.Adapters;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.EmployerOnboarding.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.EmployerOnboarding;

    /// <summary>Wires persistence, repositories, the read store, file storage/scanner adapters, the cache, the outbox publisher and the two
    /// inbox consumers (AccountApproved, EmployerVerificationApproved).</summary>
    public static IServiceCollection AddEmployerOnboardingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<EmployerOnboardingDbContext>(configuration, "EmployerOnboarding", EmployerOnboardingDbContext.Schema);
        services.AddDurableIdempotency<EmployerOnboardingDbContext>();

        services.AddScoped<IEmployerRegistrationRepository, EmployerRegistrationRepository>();
        services.AddScoped<ICompanyMediaRepository, CompanyMediaRepository>();
        services.AddScoped<IEmployerStandingRepository, EmployerStandingRepository>();
        services.AddScoped<IKnownAccountRepository, KnownAccountRepository>();
        services.AddScoped<IEmployerReadStore, EmployerReadStore>();

        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IMalwareScanner, PassthroughMalwareScanner>();
        services.AddSingleton<IEmployerCache, EmployerCache>();

        services.AddOutboxProcessor<EmployerOnboardingDbContext>(configuration);
        services.AddInboxProcessor<EmployerOnboardingDbContext>(configuration);
        services.AddInboxHandler<AccountApprovedIntegrationEvent, OpenEmployerRegistrationHandler>(ConsumerName, "AccountApproved");
        services.AddInboxHandler<EmployerVerificationApprovedIntegrationEvent, MarkEmployerVerifiedHandler>(ConsumerName, "EmployerVerificationApproved");

        return services;
    }
}
