using JobPlatform.ExternalIntegration.Application.Commands.Mapping;
using JobPlatform.ExternalIntegration.Application.DTOs.Mapping;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Mapping;

internal sealed class ConfigureJobDataMappingHandler : ICommandHandler<ConfigureJobDataMappingCommand, JobDataMappingView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IJobDataMappingRepository _mappings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ConfigureJobDataMappingHandler(IExternalJobSiteIntegrationRepository integrations, IJobDataMappingRepository mappings, ICurrentUser user,
        TimeProvider clock)
    {
        _integrations = integrations;
        _mappings = mappings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<JobDataMappingView>> Handle(ConfigureJobDataMappingCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var rules = request.Rules.Select(r => new MappingRule(r.SourceField, r.TargetField, Enum.Parse<MappingTransform>(r.Transform, true))).ToArray();
        var now = _clock.GetUtcNow().UtcDateTime;
        var actorId = ActorFactory.From(_user).Id;
        var mapping = await _mappings.GetByIntegrationAsync(integration.Id, ct);
        if (mapping is null)
        {
            mapping = JobDataMapping.Create(Guid.NewGuid(), integration.Id, rules, request.StandardSchemaVersion, actorId, now);
            _mappings.Add(mapping);
            integration.AssignMapping(mapping.Id);
        }
        else
        {
            mapping.Configure(rules, actorId, now);
        }

        return ToView(mapping);
    }

    internal static JobDataMappingView ToView(JobDataMapping m) => new(m.Id, m.IntegrationId, m.MappingVersion, m.StandardSchemaVersion,
        m.Rules.Select(r => new MappingRuleView(r.SourceField, r.TargetField, r.Transform.ToString())).ToArray());
}
