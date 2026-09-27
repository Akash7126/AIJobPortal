using FluentValidation;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application;

// ---------------------------------------------------------------------- registration & admission (US-3.4.1-01)

public sealed record RegisterExternalJobSiteCommand(string SourcePlatformName, string BaseUrl, bool RecommendedByMolPef)
    : PartnerCommand<IntegrationView>;

public sealed class RegisterExternalJobSiteValidator : AbstractValidator<RegisterExternalJobSiteCommand>
{
    public RegisterExternalJobSiteValidator()
    {
        RuleFor(c => c.SourcePlatformName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.SourcePlatformName.Required");
        RuleFor(c => c.BaseUrl).NotEmpty().Must(BeAnAbsoluteHttpsUrl).WithErrorCode("VAL.BaseUrl.Invalid");
    }

    private static bool BeAnAbsoluteHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class RegisterExternalJobSiteHandler : ICommandHandler<RegisterExternalJobSiteCommand, IntegrationView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IKnownPartnerAccountRepository _knownAccounts;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RegisterExternalJobSiteHandler(IExternalJobSiteIntegrationRepository integrations, IKnownPartnerAccountRepository knownAccounts,
        ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _knownAccounts = knownAccounts;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<IntegrationView>> Handle(RegisterExternalJobSiteCommand request, CancellationToken ct)
    {
        var partnerAccountId = _user.UserId!.Value;
        if (await _knownAccounts.GetAsync(partnerAccountId, ct) is null)
        {
            return Error.Forbidden(ErrorCodes.PartnerForbidden, "This account is not yet a recognised, active external job site.");
        }

        if (await _integrations.GetByPartnerAccountAsync(partnerAccountId, ct) is not null)
        {
            return Error.Conflict(ErrorCodes.AlreadyRegistered, "This partner account already has an integration registered.");
        }

        var platform = new SourcePlatform(Guid.NewGuid(), request.SourcePlatformName, request.BaseUrl);
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerAccountId, platform, request.RecommendedByMolPef,
            _clock.GetUtcNow().UtcDateTime);
        _integrations.Add(integration);
        return Views.ToView(integration);
    }
}

public sealed record ApproveExternalJobSiteCommand(Guid IntegrationId, string ApprovalBasis) : AdminCommand<Unit>;

public sealed class ApproveExternalJobSiteValidator : AbstractValidator<ApproveExternalJobSiteCommand>
{
    public ApproveExternalJobSiteValidator()
    {
        RuleFor(c => c.IntegrationId).NotEmpty().WithErrorCode("VAL.IntegrationId.Required");
        RuleFor(c => c.ApprovalBasis).NotEmpty().MaximumLength(500).WithErrorCode("VAL.ApprovalBasis.Required");
    }
}

internal sealed class ApproveExternalJobSiteHandler : ICommandHandler<ApproveExternalJobSiteCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ApproveExternalJobSiteHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ApproveExternalJobSiteCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByIdAsync(request.IntegrationId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The integration was not found.");
        }

        integration.ApproveByMolPef(ActorFactory.From(_user), request.ApprovalBasis, _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

public sealed record ActivateIntegrationCommand(Guid IntegrationId) : AdminCommand<Unit>;

public sealed class ActivateIntegrationValidator : AbstractValidator<ActivateIntegrationCommand>
{
    public ActivateIntegrationValidator() => RuleFor(c => c.IntegrationId).NotEmpty().WithErrorCode("VAL.IntegrationId.Required");
}

internal sealed class ActivateIntegrationHandler : ICommandHandler<ActivateIntegrationCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;

    public ActivateIntegrationHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user)
    {
        _integrations = integrations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(ActivateIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByIdAsync(request.IntegrationId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The integration was not found.");
        }

        integration.Activate(ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record SuspendIntegrationCommand(Guid IntegrationId, string Reason) : AdminCommand<Unit>;

public sealed class SuspendIntegrationValidator : AbstractValidator<SuspendIntegrationCommand>
{
    public SuspendIntegrationValidator()
    {
        RuleFor(c => c.IntegrationId).NotEmpty().WithErrorCode("VAL.IntegrationId.Required");
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500).WithErrorCode("VAL.Reason.Required");
    }
}

internal sealed class SuspendIntegrationHandler : ICommandHandler<SuspendIntegrationCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;

    public SuspendIntegrationHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user)
    {
        _integrations = integrations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(SuspendIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByIdAsync(request.IntegrationId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The integration was not found.");
        }

        integration.Suspend(ActorFactory.From(_user), request.Reason);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- partner self-service (US-2.5-02, 3.1.3-05/13, 3.4.1-05)

public sealed record EnableIntegrationCommand(bool PullEnabled, bool PushEnabled) : PartnerCommand<Unit>;

internal sealed class EnableIntegrationHandler : ICommandHandler<EnableIntegrationCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public EnableIntegrationHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(EnableIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        integration.Enable(request.PullEnabled, request.PushEnabled, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

public sealed record ConfigureSyncScheduleCommand(string Mode, string? Cron) : PartnerCommand<Unit>;

public sealed class ConfigureSyncScheduleValidator : AbstractValidator<ConfigureSyncScheduleCommand>
{
    public ConfigureSyncScheduleValidator()
    {
        RuleFor(c => c.Mode).Must(m => Enum.TryParse<SyncMode>(m, true, out _)).WithErrorCode("VAL.Mode.Invalid");
        When(c => Enum.TryParse<SyncMode>(c.Mode, true, out var mode) && mode == SyncMode.Scheduled, () =>
        {
            RuleFor(c => c.Cron).NotEmpty().WithErrorCode("VAL.Cron.Required");
            RuleFor(c => c.Cron).Must(HasAtLeastFiveMinuteInterval).When(c => !string.IsNullOrWhiteSpace(c.Cron))
                .WithErrorCode("VAL.Cron.MinimumIntervalFiveMinutes");
        });
    }

    /// <summary>Pragmatic cron validation (no cron library dependency): five space-separated fields, and the minute field must not
    /// fire more often than every 5 minutes (handover 3.4.1-05 AC-02).</summary>
    private static bool HasAtLeastFiveMinuteInterval(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return false;
        }

        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            return false;
        }

        var minute = parts[0];
        if (minute == "*")
        {
            return false;
        }

        return !minute.StartsWith("*/", StringComparison.Ordinal) || (int.TryParse(minute[2..], out var step) && step >= 5);
    }
}

internal sealed class ConfigureSyncScheduleHandler : ICommandHandler<ConfigureSyncScheduleCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;

    public ConfigureSyncScheduleHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user)
    {
        _integrations = integrations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(ConfigureSyncScheduleCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        integration.ConfigureSyncSchedule(Enum.Parse<SyncMode>(request.Mode, true), request.Cron, ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record ConfigureAttributionVisibilityCommand(string Visibility) : PartnerCommand<Unit>;

public sealed class ConfigureAttributionVisibilityValidator : AbstractValidator<ConfigureAttributionVisibilityCommand>
{
    public ConfigureAttributionVisibilityValidator() =>
        RuleFor(c => c.Visibility).Must(v => Enum.TryParse<AttributionVisibilityValue>(v, true, out _)).WithErrorCode("VAL.Visibility.Invalid");
}

internal sealed class ConfigureAttributionVisibilityHandler : ICommandHandler<ConfigureAttributionVisibilityCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ConfigureAttributionVisibilityHandler(IExternalJobSiteIntegrationRepository integrations, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ConfigureAttributionVisibilityCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        integration.ConfigureAttributionVisibility(Enum.Parse<AttributionVisibilityValue>(request.Visibility, true), ActorFactory.From(_user),
            _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

public sealed record ProvisionSandboxCommand : PartnerCommand<Unit>;

internal sealed class ProvisionSandboxHandler : ICommandHandler<ProvisionSandboxCommand, Unit>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IPartnerCredentialRepository _credentials;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ProvisionSandboxHandler(IExternalJobSiteIntegrationRepository integrations, IPartnerCredentialRepository credentials, ICurrentUser user,
        TimeProvider clock)
    {
        _integrations = integrations;
        _credentials = credentials;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ProvisionSandboxCommand request, CancellationToken ct)
    {
        var partnerAccountId = _user.UserId!.Value;
        var integration = await _integrations.GetByPartnerAccountAsync(partnerAccountId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var hasActiveCredential = await _credentials.HasActiveCredentialAsync(partnerAccountId, now, ct);
        integration.ProvisionSandbox(hasActiveCredential, ActorFactory.From(_user), now);
        return Result.Success();
    }
}

/// <summary>On-demand sync run (handover 6.1 "POST /partner/sync-runs"). Failure of the partner feed still persists the run as Failed
/// (handover 4.2), so this command must commit even when it returns an error.</summary>
public sealed record StartSyncRunCommand : PartnerCommand<SyncRunView>, IPersistOnFailure;

internal sealed class StartSyncRunHandler : ICommandHandler<StartSyncRunCommand, SyncRunView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IJobDataMappingRepository _mappings;
    private readonly PartnerSyncOrchestrator _orchestrator;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public StartSyncRunHandler(IExternalJobSiteIntegrationRepository integrations, IJobDataMappingRepository mappings,
        PartnerSyncOrchestrator orchestrator, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _mappings = mappings;
        _orchestrator = orchestrator;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<SyncRunView>> Handle(StartSyncRunCommand request, CancellationToken ct)
    {
        var partnerAccountId = _user.UserId!.Value;
        var integration = await _integrations.GetByPartnerAccountAsync(partnerAccountId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var mapping = await _mappings.GetByIntegrationAsync(integration.Id, ct);
        var actor = ActorFactory.From(_user);
        var now = _clock.GetUtcNow().UtcDateTime;
        var run = integration.StartSyncRun(SyncTrigger.OnDemand, mapping?.MappingVersion ?? 0, actor, now);

        try
        {
            var (received, accepted, rejected) = await _orchestrator.RunAsync(integration, run, actor.Id, ct);
            integration.CompleteSyncRun(run.Id, received, accepted, rejected, now);
        }
        catch (PartnerUpstreamTimeoutException ex)
        {
            integration.FailSyncRun(run.Id, ErrorCodes.UpstreamTimeout, now);
            return Error.External(ErrorCodes.UpstreamTimeout, ex.Message);
        }

        return Views.ToView(run);
    }
}

// ---------------------------------------------------------------------- queries

public sealed record GetIntegrationQuery : PartnerQuery<IntegrationView>;

internal sealed class GetIntegrationHandler : IQueryHandler<GetIntegrationQuery, IntegrationView>
{
    private readonly IExternalIntegrationReadStore _store;
    private readonly ICurrentUser _user;

    public GetIntegrationHandler(IExternalIntegrationReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<IntegrationView>> Handle(GetIntegrationQuery request, CancellationToken ct) =>
        await _store.GetIntegrationByPartnerAsync(_user.UserId!.Value, ct) is { } view
            ? view
            : Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
}

/// <summary>US-3.1.3-06/10/11 collaboration surface: BC-07/BC-09 read the integration summary rather than a live call (handover 6.1).</summary>
public sealed record GetIntegrationSummaryQuery(Guid SourcePlatformId) : ServiceQuery<IntegrationSummaryView>;

internal sealed class GetIntegrationSummaryHandler : IQueryHandler<GetIntegrationSummaryQuery, IntegrationSummaryView>
{
    private readonly IExternalIntegrationReadStore _store;

    public GetIntegrationSummaryHandler(IExternalIntegrationReadStore store) => _store = store;

    public async Task<Result<IntegrationSummaryView>> Handle(GetIntegrationSummaryQuery request, CancellationToken ct) =>
        await _store.GetIntegrationSummaryAsync(request.SourcePlatformId, ct) is { } view
            ? view
            : Error.NotFound(ErrorCodes.NotFound, "No integration was found for this source platform.");
}

internal static class Views
{
    public static IntegrationView ToView(ExternalJobSiteIntegration i) => new(
        i.Id, i.PartnerAccountId, i.SourcePlatform.Id, i.SourcePlatform.Name, i.SourcePlatform.BaseUrl, i.AdmissionStatus.ToString(),
        i.Recommendation.ToString(), i.Status.ToString(), i.Models.PullEnabled, i.Models.PushEnabled, i.Schedule.Mode.ToString(), i.Schedule.Cron,
        i.AttributionVisibility.ToString(), i.Sandbox.ToString(), i.SyncRuns.Select(ToView).ToArray(), i.RowVersion);

    public static SyncRunView ToView(SyncRun r) => new(
        r.Id, r.Trigger.ToString(), r.Status.ToString(), r.StartedAtUtc, r.EndedAtUtc, r.MappingVersion, r.Received, r.Accepted, r.Rejected,
        r.ErrorCode);
}
