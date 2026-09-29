using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Handlers.Mapping;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

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
