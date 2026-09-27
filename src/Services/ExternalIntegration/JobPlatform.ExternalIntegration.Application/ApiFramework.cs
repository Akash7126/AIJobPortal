using FluentValidation;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application;

// ---------------------------------------------------------------------- API versioning (US-3.4.3-01/02/05)

public sealed record ReleaseApiVersionCommand(string Version) : AdminCommand<ApiVersionView>;

public sealed class ReleaseApiVersionValidator : AbstractValidator<ReleaseApiVersionCommand>
{
    public ReleaseApiVersionValidator() => RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
}

internal sealed class ReleaseApiVersionHandler : ICommandHandler<ReleaseApiVersionCommand, ApiVersionView>
{
    private readonly IApiVersionRepository _versions;

    public ReleaseApiVersionHandler(IApiVersionRepository versions) => _versions = versions;

    public async Task<Result<ApiVersionView>> Handle(ReleaseApiVersionCommand request, CancellationToken ct)
    {
        if (await _versions.GetAsync(request.Version, ct) is not null)
        {
            return Error.Conflict(ErrorCodes.ApiInvalidTransition, "This API version has already been released.");
        }

        var version = Domain.ApiVersion.Release(request.Version);
        _versions.Add(version);
        return ToView(version);
    }

    internal static ApiVersionView ToView(Domain.ApiVersion v) => new(v.Id, v.Status.ToString(), v.DeprecatedAtUtc, v.SunsetAtUtc, v.AcceptedFormats);
}

/// <summary>Minimum deprecation window (proposed, handover section 7.3): 90 days.</summary>
public sealed record DeprecateApiVersionCommand(string Version, DateTime SunsetAtUtc) : AdminCommand<Unit>;

public sealed class DeprecateApiVersionValidator : AbstractValidator<DeprecateApiVersionCommand>
{
    public static readonly TimeSpan MinimumWindow = TimeSpan.FromDays(90);

    public DeprecateApiVersionValidator()
    {
        RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
        RuleFor(c => c.SunsetAtUtc).GreaterThanOrEqualTo(_ => DateTime.UtcNow + MinimumWindow).WithErrorCode("VAL.SunsetAtUtc.TooSoon");
    }
}

internal sealed class DeprecateApiVersionHandler : ICommandHandler<DeprecateApiVersionCommand, Unit>
{
    private readonly IApiVersionRepository _versions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public DeprecateApiVersionHandler(IApiVersionRepository versions, ICurrentUser user, TimeProvider clock)
    {
        _versions = versions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(DeprecateApiVersionCommand request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        version.Deprecate(request.SunsetAtUtc, _clock.GetUtcNow().UtcDateTime, ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record RetireApiVersionCommand(string Version) : AdminCommand<Unit>;

public sealed class RetireApiVersionValidator : AbstractValidator<RetireApiVersionCommand>
{
    public RetireApiVersionValidator() => RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
}

internal sealed class RetireApiVersionHandler : ICommandHandler<RetireApiVersionCommand, Unit>
{
    private readonly IApiVersionRepository _versions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RetireApiVersionHandler(IApiVersionRepository versions, ICurrentUser user, TimeProvider clock)
    {
        _versions = versions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RetireApiVersionCommand request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        version.Retire(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

public sealed record ConfigureApiDataFormatCommand(string Version, IReadOnlyList<string> Formats) : AdminCommand<Unit>;

public sealed class ConfigureApiDataFormatValidator : AbstractValidator<ConfigureApiDataFormatCommand>
{
    public ConfigureApiDataFormatValidator()
    {
        RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
        RuleFor(c => c.Formats).NotEmpty().WithErrorCode("VAL.Formats.Required");
    }
}

internal sealed class ConfigureApiDataFormatHandler : ICommandHandler<ConfigureApiDataFormatCommand, Unit>
{
    private readonly IApiVersionRepository _versions;
    private readonly ICurrentUser _user;

    public ConfigureApiDataFormatHandler(IApiVersionRepository versions, ICurrentUser user)
    {
        _versions = versions;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(ConfigureApiDataFormatCommand request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        version.ConfigureAcceptedFormats(request.Formats, ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record ListApiVersionsQuery : PublicQuery<IReadOnlyList<ApiVersionView>>;

internal sealed class ListApiVersionsHandler : IQueryHandler<ListApiVersionsQuery, IReadOnlyList<ApiVersionView>>
{
    private readonly IExternalIntegrationReadStore _store;

    public ListApiVersionsHandler(IExternalIntegrationReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<ApiVersionView>>> Handle(ListApiVersionsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListApiVersionsAsync(ct));
}

// ---------------------------------------------------------------------- documentation (US-3.1.3-12, US-3.4.3-03, US-4.3-02; handover Q-02/Q-03)

/// <summary>Modelled as a command (not a plain query) because viewing the schema is a side effect: it logs the view and publishes
/// ApiSchemaDocumentationViewed (handover Q-03) — a deliberate deviation from "GET routes are queries" to reuse the pipeline's
/// unit-of-work commit; see the BC-02 status doc.</summary>
public sealed record ViewApiSchemaDocumentationCommand(string Version) : PartnerCommand<ApiSchemaDocumentationView>;

public sealed class ViewApiSchemaDocumentationValidator : AbstractValidator<ViewApiSchemaDocumentationCommand>
{
    public ViewApiSchemaDocumentationValidator() => RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
}

internal sealed class ViewApiSchemaDocumentationHandler : ICommandHandler<ViewApiSchemaDocumentationCommand, ApiSchemaDocumentationView>
{
    private readonly IApiVersionRepository _versions;
    private readonly IApiSchemaAccessLogRepository _logs;
    private readonly IPartnerCredentialRepository _credentials;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ViewApiSchemaDocumentationHandler(IApiVersionRepository versions, IApiSchemaAccessLogRepository logs,
        IPartnerCredentialRepository credentials, ICurrentUser user, TimeProvider clock)
    {
        _versions = versions;
        _logs = logs;
        _credentials = credentials;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<ApiSchemaDocumentationView>> Handle(ViewApiSchemaDocumentationCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (!await _credentials.HasActiveCredentialAsync(_user.UserId ?? Guid.Empty, now, ct))
        {
            return Error.Forbidden(ErrorCodes.PartnerForbidden, "An active API credential is required to view the schema documentation.");
        }

        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        var actorId = ActorFactory.From(_user).Id;
        _logs.Add(ApiSchemaAccessLog.Record(Guid.NewGuid(), request.Version, actorId, now));

        var schemaJson = GetStandardSchemaHandler.Schema.Fields
            .Select(f => $"\"{f.Field}\":{{\"required\":{f.Required.ToString().ToLowerInvariant()}}}");
        return new ApiSchemaDocumentationView(request.Version, "{" + string.Join(",", schemaJson) + "}",
            version.Status == ApiVersionStatus.Deprecated, version.SunsetAtUtc);
    }
}

public sealed record GetApiDocumentationQuery(string Version) : PublicQuery<ApiDocumentationView>;

internal sealed class GetApiDocumentationHandler : IQueryHandler<GetApiDocumentationQuery, ApiDocumentationView>
{
    private readonly IApiVersionRepository _versions;

    public GetApiDocumentationHandler(IApiVersionRepository versions) => _versions = versions;

    public async Task<Result<ApiDocumentationView>> Handle(GetApiDocumentationQuery request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        var content = $"JobPlatform External Integration API {version.Id} — see /openapi/v1.json for the full contract.";
        return new ApiDocumentationView(version.Id, content, version.Status == ApiVersionStatus.Deprecated, version.SunsetAtUtc);
    }
}

public sealed record GetSoftwareInterfaceDocumentationQuery(string Version) : PartnerQuery<IReadOnlyList<SoftwareInterfaceView>>;

internal sealed class GetSoftwareInterfaceDocumentationHandler
    : IQueryHandler<GetSoftwareInterfaceDocumentationQuery, IReadOnlyList<SoftwareInterfaceView>>
{
    private readonly IApiVersionRepository _versions;
    private readonly IExternalIntegrationReadStore _store;

    public GetSoftwareInterfaceDocumentationHandler(IApiVersionRepository versions, IExternalIntegrationReadStore store)
    {
        _versions = versions;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<SoftwareInterfaceView>>> Handle(GetSoftwareInterfaceDocumentationQuery request, CancellationToken ct)
    {
        if (await _versions.GetAsync(request.Version, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        return Result.Success(await _store.ListSoftwareInterfacesAsync(ct));
    }
}

// ---------------------------------------------------------------------- software interface registry (US-4.3-01)

public sealed record RegisterSoftwareInterfaceCommand(string Category, string Name, string Endpoint) : AdminCommand<SoftwareInterfaceView>;

public sealed class RegisterSoftwareInterfaceValidator : AbstractValidator<RegisterSoftwareInterfaceCommand>
{
    public RegisterSoftwareInterfaceValidator()
    {
        RuleFor(c => c.Category).Must(v => Enum.TryParse<SoftwareInterfaceCategory>(v, true, out _)).WithErrorCode("VAL.Category.Invalid");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Name.Required");
        RuleFor(c => c.Endpoint).Must(BeAnAbsoluteHttpsUrl).WithErrorCode("VAL.Endpoint.Invalid");
    }

    private static bool BeAnAbsoluteHttpsUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class RegisterSoftwareInterfaceHandler : ICommandHandler<RegisterSoftwareInterfaceCommand, SoftwareInterfaceView>
{
    private readonly ISoftwareInterfaceRepository _interfaces;
    private readonly ICurrentUser _user;

    public RegisterSoftwareInterfaceHandler(ISoftwareInterfaceRepository interfaces, ICurrentUser user)
    {
        _interfaces = interfaces;
        _user = user;
    }

    public async Task<Result<SoftwareInterfaceView>> Handle(RegisterSoftwareInterfaceCommand request, CancellationToken ct)
    {
        var category = Enum.Parse<SoftwareInterfaceCategory>(request.Category, true);
        var actor = ActorFactory.From(_user);
        var existing = await _interfaces.GetByKeyAsync(category, request.Name, ct);
        if (existing is not null)
        {
            // AC-05: idempotent — a repeat registration updates the endpoint rather than creating a duplicate.
            existing.UpdateEndpoint(request.Endpoint, actor);
            return ToView(existing);
        }

        var connection = SoftwareInterfaceConnection.Register(Guid.NewGuid(), category, request.Name, request.Endpoint, actor);
        _interfaces.Add(connection);
        return ToView(connection);
    }

    private static SoftwareInterfaceView ToView(SoftwareInterfaceConnection c) => new(c.Id, c.Category.ToString(), c.Name, c.Endpoint, c.Enabled);
}
