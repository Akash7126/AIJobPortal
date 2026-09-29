using JobPlatform.ExternalIntegration.Application.DTOs.Mapping;

namespace JobPlatform.ExternalIntegration.Application.Commands.Mapping;

public sealed record ConfigureJobDataMappingCommand(IReadOnlyList<MappingRuleInput> Rules, string StandardSchemaVersion) : PartnerCommand<JobDataMappingView>;
