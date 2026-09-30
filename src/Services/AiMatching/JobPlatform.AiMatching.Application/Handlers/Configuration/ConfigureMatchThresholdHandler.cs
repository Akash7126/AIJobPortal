using JobPlatform.AiMatching.Application.Commands.Configuration;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Configuration;

internal sealed class ConfigureMatchThresholdHandler(IMatchingConfigurationRepository repository, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ConfigureMatchThresholdCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ConfigureMatchThresholdCommand request, CancellationToken ct)
    {
        var configuration = await repository.GetCurrentAsync(ct);
        if (configuration is null)
        {
            return Error.NotFound(AiErrorCodes.NotFound, "The matching configuration has not been initialised.");
        }

        if (!ETag.Matches(request.IfMatch, configuration.RowVersion))
        {
            return Error.PreconditionFailed("E-PRECONDITION-FAILED", "The configuration changed since it was read.");
        }

        configuration.ChangeThreshold(request.ThresholdPercent, user.ToActor(), clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
