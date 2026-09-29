using JobPlatform.ExternalIntegration.Application.DTOs.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;

/// <summary>The push model (handover Q-01): the partner posts a job already shaped like the platform's standard schema, so no
/// JobDataMapping translation is needed on this path (mapping only translates a pull source's own, non-standard shape).</summary>
public sealed record PushJobDataCommand(
    string SourceJobId, string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
    DateTime ApplicationDeadline, string Location, string? SourceUrl, string? IdempotencyKey)
    : PartnerCommand<PushJobDataResultView>, IIdempotentCommand, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ErrorCodes.JobDataInvalidField;
}
