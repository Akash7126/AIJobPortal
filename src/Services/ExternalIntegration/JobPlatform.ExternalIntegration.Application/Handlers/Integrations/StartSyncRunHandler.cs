using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.ExternalIntegration.Application.Services.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

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
