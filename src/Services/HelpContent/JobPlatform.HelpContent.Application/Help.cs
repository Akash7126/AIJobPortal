using FluentValidation;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application;

// ---------------------------------------------------------------------- commands: authoring

public sealed record CreateHelpContentCommand(HelpKind Kind, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn) : AdminCommand<HelpContentView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

public sealed class CreateHelpContentValidator : AbstractValidator<CreateHelpContentCommand>
{
    public CreateHelpContentValidator()
    {
        Include(new TitleBodyRules<CreateHelpContentCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("VAL.Kind.Invalid");
    }
}

internal sealed class CreateHelpContentHandler : ICommandHandler<CreateHelpContentCommand, HelpContentView>
{
    private readonly IHelpContentRepository _content;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreateHelpContentHandler(IHelpContentRepository content, ICurrentUser user, TimeProvider clock)
    {
        _content = content;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<HelpContentView>> Handle(CreateHelpContentCommand request, CancellationToken ct)
    {
        var content = Domain.HelpContent.Create(Guid.NewGuid(), request.Kind, new LocalizedText(request.TitleAr, request.TitleEn),
            new LocalizedText(request.BodyAr, request.BodyEn), ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _content.Add(content);
        return await Task.FromResult(ToView(content));
    }

    internal static HelpContentView ToView(Domain.HelpContent c, Guid? topicId = null, IReadOnlyList<HelpRole>? roles = null) => new(
        c.Id, c.Kind.ToString(), new LocalizedView(c.Current.Title.Ar, c.Current.Title.En), new LocalizedView(c.Current.Body.Ar, c.Current.Body.En),
        c.CurrentVersion, topicId, (roles ?? Array.Empty<HelpRole>()).Select(r => r.ToString()).ToArray(),
        c.Media.Select(m => new HelpMediaView(m.Id, m.Type.ToString(), m.StorageKey, m.CaptionsRef, m.TextAlternative)).ToArray(), c.RowVersion);
}

public sealed record UpdateHelpContentCommand(Guid HelpContentId, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn)
    : AdminCommand<HelpContentView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

public sealed class UpdateHelpContentValidator : AbstractValidator<UpdateHelpContentCommand>
{
    public UpdateHelpContentValidator() => Include(new TitleBodyRules<UpdateHelpContentCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
}

internal sealed class UpdateHelpContentHandler : ICommandHandler<UpdateHelpContentCommand, HelpContentView>
{
    private readonly IHelpContentRepository _content;
    private readonly IHelpOrganizationRepository _organizations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdateHelpContentHandler(IHelpContentRepository content, IHelpOrganizationRepository organizations, ICurrentUser user, TimeProvider clock)
    {
        _content = content;
        _organizations = organizations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<HelpContentView>> Handle(UpdateHelpContentCommand request, CancellationToken ct)
    {
        var content = await _content.GetByIdAsync(request.HelpContentId, ct);
        if (content is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        content.Update(new LocalizedText(request.TitleAr, request.TitleEn), new LocalizedText(request.BodyAr, request.BodyEn), ActorFactory.From(_user),
            _clock.GetUtcNow().UtcDateTime);
        var organization = await _organizations.GetAsync(content.Id, ct);
        return CreateHelpContentHandler.ToView(content, organization?.TopicId, organization?.Roles);
    }
}

public sealed record AttachHelpMediaCommand(Guid HelpContentId, HelpMediaType Type, string FileName, string ContentType, long SizeBytes, byte[] Content,
    string? CaptionsRef, string? TextAlternative) : AdminCommand<HelpMediaView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

public sealed class AttachHelpMediaValidator : AbstractValidator<AttachHelpMediaCommand>
{
    public AttachHelpMediaValidator()
    {
        RuleFor(c => c.Type).IsInEnum().WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(c.CaptionsRef) || !string.IsNullOrWhiteSpace(c.TextAlternative))
            .WithErrorCode("VAL.Captions.Required").OverridePropertyName(nameof(AttachHelpMediaCommand.CaptionsRef));
    }
}

internal sealed class AttachHelpMediaHandler : ICommandHandler<AttachHelpMediaCommand, HelpMediaView>
{
    private readonly IHelpContentRepository _content;
    private readonly IMediaStorage _storage;
    private readonly ICurrentUser _user;

    public AttachHelpMediaHandler(IHelpContentRepository content, IMediaStorage storage, ICurrentUser user)
    {
        _content = content;
        _storage = storage;
        _user = user;
    }

    public async Task<Result<HelpMediaView>> Handle(AttachHelpMediaCommand request, CancellationToken ct)
    {
        var content = await _content.GetByIdAsync(request.HelpContentId, ct);
        if (content is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        await using var stream = new MemoryStream(request.Content);
        var storageKey = await _storage.SaveAsync(stream, request.FileName, request.ContentType, ct);
        var media = content.AttachMedia(Guid.NewGuid(), request.Type, storageKey, request.CaptionsRef, request.TextAlternative, ActorFactory.From(_user));
        return new HelpMediaView(media.Id, media.Type.ToString(), _storage.UrlFor(storageKey), media.CaptionsRef, media.TextAlternative);
    }
}

// ---------------------------------------------------------------------- commands: organization (topics/roles)

public sealed record UpdateHelpContentOrganizationCommand(Guid HelpContentId, Guid? TopicId, IReadOnlyList<HelpRole> Roles) : AdminCommand<Unit>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

internal sealed class UpdateHelpContentOrganizationHandler : ICommandHandler<UpdateHelpContentOrganizationCommand, Unit>
{
    private readonly IHelpContentRepository _content;
    private readonly IHelpOrganizationRepository _organizations;
    private readonly ICurrentUser _user;

    public UpdateHelpContentOrganizationHandler(IHelpContentRepository content, IHelpOrganizationRepository organizations, ICurrentUser user)
    {
        _content = content;
        _organizations = organizations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(UpdateHelpContentOrganizationCommand request, CancellationToken ct)
    {
        if (await _content.GetByIdAsync(request.HelpContentId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        var organization = await _organizations.GetAsync(request.HelpContentId, ct);
        if (organization is null)
        {
            organization = HelpContentOrganization.CreateFor(request.HelpContentId);
            _organizations.Add(organization);
        }

        organization.AssignTopicAndRoles(request.TopicId, request.Roles, ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record CreateHelpTopicCommand(string? NameAr, string? NameEn) : AdminCommand<HelpTopicView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

internal sealed class CreateHelpTopicHandler : ICommandHandler<CreateHelpTopicCommand, HelpTopicView>
{
    private readonly IHelpTopicRepository _topics;
    private readonly ICurrentUser _user;

    public CreateHelpTopicHandler(IHelpTopicRepository topics, ICurrentUser user)
    {
        _topics = topics;
        _user = user;
    }

    public async Task<Result<HelpTopicView>> Handle(CreateHelpTopicCommand request, CancellationToken ct)
    {
        var topic = HelpTopic.Create(Guid.NewGuid(), new LocalizedText(request.NameAr, request.NameEn), ActorFactory.From(_user));
        _topics.Add(topic);
        return await Task.FromResult(new HelpTopicView(topic.Id, new LocalizedView(topic.Name.Ar, topic.Name.En), topic.IsRemoved));
    }
}

public sealed record RemoveHelpTopicCommand(Guid TopicId) : AdminCommand<Unit>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

internal sealed class RemoveHelpTopicHandler : ICommandHandler<RemoveHelpTopicCommand, Unit>
{
    private readonly IHelpTopicRepository _topics;
    private readonly ICurrentUser _user;

    public RemoveHelpTopicHandler(IHelpTopicRepository topics, ICurrentUser user)
    {
        _topics = topics;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(RemoveHelpTopicCommand request, CancellationToken ct)
    {
        var topic = await _topics.GetByIdAsync(request.TopicId, ct);
        if (topic is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The topic was not found.");
        }

        topic.Remove(ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record ListHelpTopicsQuery : AdminQuery<IReadOnlyList<HelpTopicView>>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

internal sealed class ListHelpTopicsHandler : IQueryHandler<ListHelpTopicsQuery, IReadOnlyList<HelpTopicView>>
{
    private readonly IHelpContentReadStore _store;

    public ListHelpTopicsHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<HelpTopicView>>> Handle(ListHelpTopicsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListHelpTopicsAsync(ct));
}

// ---------------------------------------------------------------------- queries: public reads

public sealed record GetHelpContentQuery(Guid HelpContentId) : IQuery<HelpContentView>;

internal sealed class GetHelpContentHandler : IQueryHandler<GetHelpContentQuery, HelpContentView>
{
    private readonly IHelpContentReadStore _store;

    public GetHelpContentHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<HelpContentView>> Handle(GetHelpContentQuery request, CancellationToken ct) =>
        await _store.GetHelpContentAsync(request.HelpContentId, ct) is { } view ? view : Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
}

/// <summary>US-3.7.2-02: content is shown only to its assigned roles - Guest/null means every visitor. "no role assigned" behaves like Guest.</summary>
public sealed record GetHelpCenterQuery(HelpRole? Role) : IQuery<IReadOnlyList<HelpCenterTopicView>>;

internal sealed class GetHelpCenterHandler : IQueryHandler<GetHelpCenterQuery, IReadOnlyList<HelpCenterTopicView>>
{
    private readonly IHelpContentReadStore _store;

    public GetHelpCenterHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<HelpCenterTopicView>>> Handle(GetHelpCenterQuery request, CancellationToken ct) =>
        Result.Success(await _store.GetHelpCenterAsync(request.Role, ct));
}

/// <summary>US-3.7.2-01/03: "no results" is a normal outcome (AC-02), never an error.</summary>
public sealed record SearchHelpContentQuery(string Keyword, HelpRole? Role, int Page = 1, int PageSize = 20) : IQuery<PagedResult<HelpSearchResultView>>;

public sealed class SearchHelpContentValidator : AbstractValidator<SearchHelpContentQuery>
{
    public SearchHelpContentValidator()
    {
        RuleFor(c => c.Keyword).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Keyword.Required");
        RuleFor(c => c.PageSize).LessThanOrEqualTo(50).WithErrorCode("VAL.PageSize.TooLarge");
    }
}

internal sealed class SearchHelpContentHandler : IQueryHandler<SearchHelpContentQuery, PagedResult<HelpSearchResultView>>
{
    private readonly IHelpContentReadStore _store;

    public SearchHelpContentHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<PagedResult<HelpSearchResultView>>> Handle(SearchHelpContentQuery request, CancellationToken ct) =>
        await _store.SearchHelpContentAsync(request.Keyword, request.Role, new PageRequest(request.Page, request.PageSize), ct);
}

/// <summary>US-3.7.2-04: an unmapped page key falls back to the general help center rather than an error (AC-02).</summary>
public sealed record GetContextHelpQuery(string PageKey) : IQuery<HelpContentView?>;

internal sealed class GetContextHelpHandler : IQueryHandler<GetContextHelpQuery, HelpContentView?>
{
    private readonly IContextHelpMappingRepository _mappings;
    private readonly IHelpContentReadStore _store;

    public GetContextHelpHandler(IContextHelpMappingRepository mappings, IHelpContentReadStore store)
    {
        _mappings = mappings;
        _store = store;
    }

    public async Task<Result<HelpContentView?>> Handle(GetContextHelpQuery request, CancellationToken ct)
    {
        var mapping = await _mappings.GetByPageKeyAsync(request.PageKey, ct);
        return mapping is null ? Result.Success<HelpContentView?>(null) : Result.Success(await _store.GetHelpContentAsync(mapping.HelpContentId, ct));
    }
}

public sealed record SetContextHelpMappingCommand(string PageKey, Guid HelpContentId) : AdminCommand<Unit>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

internal sealed class SetContextHelpMappingHandler : ICommandHandler<SetContextHelpMappingCommand, Unit>
{
    private readonly IContextHelpMappingRepository _mappings;
    private readonly IHelpContentRepository _content;
    private readonly ICurrentUser _user;

    public SetContextHelpMappingHandler(IContextHelpMappingRepository mappings, IHelpContentRepository content, ICurrentUser user)
    {
        _mappings = mappings;
        _content = content;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(SetContextHelpMappingCommand request, CancellationToken ct)
    {
        if (await _content.GetByIdAsync(request.HelpContentId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        var actor = ActorFactory.From(_user);
        var mapping = await _mappings.GetByPageKeyAsync(request.PageKey, ct);
        if (mapping is null)
        {
            _mappings.Add(ContextHelpMapping.Create(Guid.NewGuid(), request.PageKey, request.HelpContentId, actor));
        }
        else
        {
            mapping.Repoint(request.HelpContentId, actor);
        }

        return Result.Success();
    }
}
