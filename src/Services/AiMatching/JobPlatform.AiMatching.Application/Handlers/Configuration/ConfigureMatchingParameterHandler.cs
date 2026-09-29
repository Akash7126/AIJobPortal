using JobPlatform.AiMatching.Application.Commands.Configuration;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Configuration;

internal sealed class ConfigureMatchingParameterHandler(IMatchingConfigurationRepository repository, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ConfigureMatchingParameterCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ConfigureMatchingParameterCommand request, CancellationToken ct)
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

        var weights = CriterionWeights.Create(request.SkillOverlap, request.Education, request.Training, request.Location, request.Experience, request.Salary);
        configuration.ChangeWeights(weights, user.ToActor(), clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
