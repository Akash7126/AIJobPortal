namespace JobPlatform.HelpContent.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface INewsArticleRepository
{
    Task<NewsArticle?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>INV-02: looks up an existing draft by its content hash before a new one is created.</summary>
    Task<NewsArticle?> GetDraftByContentHashAsync(string contentHash, CancellationToken ct = default);

    void Add(NewsArticle article);
}

public interface IContentCategoryRepository
{
    Task<ContentCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ContentCategory>> ListAsync(CancellationToken ct = default);

    void Add(ContentCategory category);
}

public interface IContentCategorizationRepository
{
    Task<ContentCategorization?> GetByArticleAsync(Guid articleId, CancellationToken ct = default);

    void Add(ContentCategorization categorization);
}

public interface IHelpContentRepository
{
    Task<HelpContent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    void Add(HelpContent content);
}

public interface IHelpTopicRepository
{
    Task<HelpTopic?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<HelpTopic>> ListAsync(CancellationToken ct = default);

    void Add(HelpTopic topic);
}

public interface IHelpOrganizationRepository
{
    Task<HelpContentOrganization?> GetAsync(Guid helpContentId, CancellationToken ct = default);

    void Add(HelpContentOrganization organization);
}

public interface IHelpFeedbackRepository
{
    Task<HelpFeedback?> GetByUserAndContentAsync(Guid userId, Guid helpContentId, CancellationToken ct = default);

    void Add(HelpFeedback feedback);
}

public interface ITutorialProgressRepository
{
    Task<TutorialProgress?> GetAsync(Guid userId, Guid tutorialId, CancellationToken ct = default);

    void Add(TutorialProgress progress);
}

public interface IContextHelpMappingRepository
{
    Task<ContextHelpMapping?> GetByPageKeyAsync(string pageKey, CancellationToken ct = default);

    void Add(ContextHelpMapping mapping);
}

public interface ICompanyProfilePageRepository
{
    Task<CompanyProfilePage?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(CompanyProfilePage page);
}
