using JobPlatform.ExternalIntegration.Application.DTOs.Mapping;
using JobPlatform.ExternalIntegration.Application.Queries.Mapping;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Mapping;

internal sealed class GetJobDataMappingHandler : IQueryHandler<GetJobDataMappingQuery, JobDataMappingView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IJobDataMappingRepository _mappings;
    private readonly ICurrentUser _user;

    public GetJobDataMappingHandler(IExternalJobSiteIntegrationRepository integrations, IJobDataMappingRepository mappings, ICurrentUser user)
    {
        _integrations = integrations;
        _mappings = mappings;
        _user = user;
    }

    public async Task<Result<JobDataMappingView>> Handle(GetJobDataMappingQuery request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var mapping = await _mappings.GetByIntegrationAsync(integration.Id, ct);
        return mapping is null
            ? Error.NotFound(ErrorCodes.NotFound, "No mapping has been configured for this integration yet.")
            : ConfigureJobDataMappingHandler.ToView(mapping);
    }
}
