namespace JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

public sealed record PurgeExpiredGovernmentDataCommand(int Take = 100) : ServiceCommand<int>;
