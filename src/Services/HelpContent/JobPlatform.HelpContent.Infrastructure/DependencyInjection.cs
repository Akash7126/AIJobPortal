using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Infrastructure.Adapters;
using JobPlatform.HelpContent.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.HelpContent.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.HelpContent;

    /// <summary>Wires persistence, repositories, the read store, media storage, the cache, the outbox publisher (BC-06 consumes no
    /// integration events, handover section 5.2) and the three cross-BC providers (BC-05 company info, BC-09 open postings, BC-04 profile
    /// interests), chosen by &lt;Port&gt;:Provider = Http | Fake exactly like BC-09's own cross-BC clients.</summary>
    public static IServiceCollection AddHelpContentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<HelpContentDbContext>(configuration, "HelpContent", HelpContentDbContext.Schema);
        services.AddDurableIdempotency<HelpContentDbContext>();

        services.AddScoped<INewsArticleRepository, NewsArticleRepository>();
        services.AddScoped<IContentCategoryRepository, ContentCategoryRepository>();
        services.AddScoped<IContentCategorizationRepository, ContentCategorizationRepository>();
        services.AddScoped<IHelpContentRepository, HelpContentRepository>();
        services.AddScoped<IHelpTopicRepository, HelpTopicRepository>();
        services.AddScoped<IHelpOrganizationRepository, HelpOrganizationRepository>();
        services.AddScoped<IHelpFeedbackRepository, HelpFeedbackRepository>();
        services.AddScoped<ITutorialProgressRepository, TutorialProgressRepository>();
        services.AddScoped<IContextHelpMappingRepository, ContextHelpMappingRepository>();
        services.AddScoped<ICompanyProfilePageRepository, CompanyProfilePageRepository>();
        services.AddScoped<IHelpContentReadStore, HelpContentReadStore>();

        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddSingleton<IHelpContentCache, HelpContentCache>();

        services.AddOutboxProcessor<HelpContentDbContext>(configuration);

        AddCompanyDirectoryProvider(services);
        AddOpenPostingsProvider(services);
        AddProfileInterestsProvider(services);

        return services;
    }

    private static void AddCompanyDirectoryProvider(IServiceCollection services)
    {
        services.AddSingleton<FakeCompanyDirectoryProvider>();
        services.AddInternalApiClient<HttpCompanyDirectoryProvider, HttpCompanyDirectoryProvider>("Downstream:EmployerOnboarding:BaseUrl");
        services.AddScoped<ICompanyDirectoryProvider>(sp => IsProvider(sp, "CompanyDirectory:Provider", "Http")
            ? sp.GetRequiredService<HttpCompanyDirectoryProvider>()
            : sp.GetRequiredService<FakeCompanyDirectoryProvider>());
    }

    private static void AddOpenPostingsProvider(IServiceCollection services)
    {
        services.AddSingleton<FakeOpenPostingsProvider>();
        services.AddInternalApiClient<HttpOpenPostingsProvider, HttpOpenPostingsProvider>("Downstream:JobPosting:BaseUrl");
        services.AddScoped<IOpenPostingsProvider>(sp => IsProvider(sp, "OpenPostings:Provider", "Http")
            ? sp.GetRequiredService<HttpOpenPostingsProvider>()
            : sp.GetRequiredService<FakeOpenPostingsProvider>());
    }

    private static void AddProfileInterestsProvider(IServiceCollection services)
    {
        services.AddSingleton<FakeProfileInterestsProvider>();
        services.AddInternalApiClient<HttpProfileInterestsProvider, HttpProfileInterestsProvider>("Downstream:JobSeekerProfile:BaseUrl");
        services.AddScoped<IProfileInterestsProvider>(sp => IsProvider(sp, "ProfileInterests:Provider", "Http")
            ? sp.GetRequiredService<HttpProfileInterestsProvider>()
            : sp.GetRequiredService<FakeProfileInterestsProvider>());
    }

    private static bool IsProvider(IServiceProvider sp, string key, string expected) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[key], expected, StringComparison.OrdinalIgnoreCase);
}
