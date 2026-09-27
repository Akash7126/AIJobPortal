using FluentValidation;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application;

// ---------------------------------------------------------------------- requests (US-3.3.1-04 threshold, US-3.3.1-07 weights)

public sealed record GetMatchingConfigurationQuery : AdminRequest, IQuery<MatchingConfigurationDto>;

public sealed record ConfigureMatchThresholdCommand(decimal ThresholdPercent, string? IfMatch = null) : AdminRequest, ICommand;

public sealed record ConfigureMatchingParameterCommand(
    decimal SkillOverlap, decimal Education, decimal Training, decimal Location, decimal Experience, decimal Salary, string? IfMatch = null) : AdminRequest, ICommand;

// ---------------------------------------------------------------------- validators (malformed input = 400; the domain re-checks)

public sealed class ConfigureMatchThresholdValidator : AbstractValidator<ConfigureMatchThresholdCommand>
{
    public ConfigureMatchThresholdValidator() =>
        RuleFor(x => x.ThresholdPercent).InclusiveBetween(0m, 100m).WithErrorCode("VAL.ThresholdPercent.OutOfRange");
}

public sealed class ConfigureMatchingParameterValidator : AbstractValidator<ConfigureMatchingParameterCommand>
{
    public ConfigureMatchingParameterValidator()
    {
        RuleFor(x => x.SkillOverlap).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Education).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Training).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Location).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Experience).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Salary).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x).Must(x => Math.Abs(x.SkillOverlap + x.Education + x.Training + x.Location + x.Experience + x.Salary - 100m) <= CriterionWeights.Epsilon)
            .OverridePropertyName("weights").WithErrorCode("VAL.Weights.SumNot100");
    }
}

// ---------------------------------------------------------------------- handlers

internal sealed class GetMatchingConfigurationHandler(IMatchReadStore store) : IQueryHandler<GetMatchingConfigurationQuery, MatchingConfigurationDto>
{
    public async Task<Result<MatchingConfigurationDto>> Handle(GetMatchingConfigurationQuery request, CancellationToken ct) =>
        await store.GetConfigurationAsync(ct) is { } dto
            ? dto
            : Error.NotFound(AiErrorCodes.NotFound, "The matching configuration has not been initialised.");
}

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

/// <summary>A configuration change applies to the next computation: evict the cached snapshot right after the commit (cache key config:current).</summary>
internal sealed class ConfigurationChangedHandler(IMatchingConfigurationProvider provider) : IDomainEventHandler<MatchingConfigurationChangedDomainEvent>
{
    public Task Handle(MatchingConfigurationChangedDomainEvent domainEvent, CancellationToken ct) => provider.InvalidateAsync(ct);
}
