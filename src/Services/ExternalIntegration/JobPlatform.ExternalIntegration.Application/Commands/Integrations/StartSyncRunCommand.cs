using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

/// <summary>On-demand sync run (handover 6.1 "POST /partner/sync-runs"). Failure of the partner feed still persists the run as Failed
/// (handover 4.2), so this command must commit even when it returns an error.</summary>
public sealed record StartSyncRunCommand : PartnerCommand<SyncRunView>, IPersistOnFailure;
