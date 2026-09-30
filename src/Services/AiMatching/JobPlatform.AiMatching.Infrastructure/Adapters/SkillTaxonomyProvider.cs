using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.PlatformAdministration;
using JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Infrastructure.Adapters;

/// <summary>
/// Skills taxonomy from BC-08 (GET /internal/v1/taxonomies/skills), cached for 60 minutes and evicted by PlatformTaxonomyUpdated. Configuration
/// Taxonomy:Provider = Http | Builtin; when BC-08 is unavailable (or Builtin is chosen) a small built-in taxonomy keeps standardisation working.
/// A run captures one taxonomy object, so a version published mid-run never changes it.
/// </summary>
internal sealed class SkillTaxonomyProvider(IPlatformAdministrationApi api, ICacheStore cache, IConfiguration configuration, ILogger<SkillTaxonomyProvider> logger)
    : ISkillTaxonomyProvider
{
    private const string CurrentKey = "taxonomy:skills:current";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(60);

    public async Task<SkillTaxonomy> GetAsync(string? version, CancellationToken ct = default)
    {
        if (!string.Equals(configuration["Taxonomy:Provider"], "Http", StringComparison.OrdinalIgnoreCase))
        {
            return BuiltinSkillTaxonomy.Create();
        }

        var key = version is null ? CurrentKey : $"taxonomy:skills:v{version}";
        TaxonomyDto? dto = null;
        try
        {
            dto = await cache.GetJsonAsync<TaxonomyDto>(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Taxonomy cache read failed; reading from BC-08");
        }

        if (dto is null)
        {
            try
            {
                dto = await api.GetTaxonomyAsync("skills", version, ct);
                if (dto is not null)
                {
                    await cache.SetJsonAsync(version is null ? CurrentKey : key, dto, Ttl, ct);
                    await cache.SetJsonAsync($"taxonomy:skills:v{dto.Version}", dto, Ttl, ct);
                }
            }
            catch (Exception ex) when (ex is InternalApiException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Skills taxonomy unavailable from BC-08; using the built-in taxonomy");
            }
        }

        return dto is null
            ? BuiltinSkillTaxonomy.Create()
            : new SkillTaxonomy(dto.Version, dto.Entries.Where(e => e.IsActive).Select(e => new TaxonomySkill(e.Code, e.LabelEn, e.LabelAr, e.Synonyms)));
    }

    public async Task InvalidateAsync(CancellationToken ct = default) => await cache.RemoveAsync(CurrentKey, ct);
}

/// <summary>Small fallback taxonomy (version "builtin-1"): common skills with English/Arabic labels and synonyms.</summary>
public static class BuiltinSkillTaxonomy
{
    public const string Version = "builtin-1";

    public static SkillTaxonomy Create() => new(Version, new[]
    {
        Skill("CSHARP", "C#", "سي شارب", "csharp", "c sharp", "dotnet c#"),
        Skill("DOTNET", ".NET", "دوت نت", "dotnet", "asp.net", ".net core"),
        Skill("JAVA", "Java", "جافا"),
        Skill("JAVASCRIPT", "JavaScript", "جافا سكريبت", "js", "ecmascript"),
        Skill("PYTHON", "Python", "بايثون"),
        Skill("SQL", "SQL", "قواعد البيانات", "sql server", "mysql", "postgresql"),
        Skill("EXCEL", "Excel", "إكسل", "microsoft excel", "ms excel"),
        Skill("ACCOUNTING", "Accounting", "المحاسبة", "bookkeeping", "محاسبة"),
        Skill("PROJECT_MGMT", "Project Management", "إدارة المشاريع", "pmp", "project manager"),
        Skill("COMMUNICATION", "Communication", "التواصل", "communication skills", "مهارات التواصل"),
        Skill("TEAMWORK", "Teamwork", "العمل الجماعي", "team work", "العمل ضمن فريق"),
        Skill("ENGLISH", "English", "اللغة الإنجليزية", "english language"),
        Skill("ARABIC", "Arabic", "اللغة العربية", "arabic language"),
        Skill("SALES", "Sales", "المبيعات", "selling"),
        Skill("CUSTOMER_SERVICE", "Customer Service", "خدمة العملاء", "customer support"),
        Skill("TEACHING", "Teaching", "التدريس", "تعليم"),
        Skill("NURSING", "Nursing", "التمريض"),
        Skill("AUTOCAD", "AutoCAD", "أوتوكاد", "auto cad"),
        Skill("MARKETING", "Marketing", "التسويق", "digital marketing"),
        Skill("DATA_ANALYSIS", "Data Analysis", "تحليل البيانات", "data analytics", "power bi")
    });

    private static TaxonomySkill Skill(string code, string en, string ar, params string[] synonyms) => new(code, en, ar, synonyms);
}
