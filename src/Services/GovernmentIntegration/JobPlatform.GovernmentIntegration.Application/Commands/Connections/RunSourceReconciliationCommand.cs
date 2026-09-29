using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Connections;

public sealed record RunSourceReconciliationCommand(SourceSystem Source) : ServiceCommand<Unit>;
