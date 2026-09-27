using FluentValidation;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Preferences;

public sealed record ConfigureJobPreferenceCommand(
    IReadOnlyList<string> JobTypes, IReadOnlyList<string> Industries, IReadOnlyList<string> Locations, decimal? SalaryMin, decimal? SalaryMax,
    string? SalaryCurrency, IReadOnlyList<string> WorkArrangements) : JobSeekerCommand<Unit>;

public sealed record GetJobPreferenceQuery : JobSeekerQuery<JobPreferenceView>;

public sealed class ConfigureJobPreferenceValidator : AbstractValidator<ConfigureJobPreferenceCommand>
{
    public ConfigureJobPreferenceValidator()
    {
        RuleFor(c => c.JobTypes).Must(l => l.Count <= 20).WithErrorCode("VAL.JobTypes.TooMany");
        RuleFor(c => c.Industries).Must(l => l.Count <= 20).WithErrorCode("VAL.Industries.TooMany");
        RuleFor(c => c.Locations).Must(l => l.Count <= 20).WithErrorCode("VAL.Locations.TooMany");
        RuleFor(c => c).Must(c => c.SalaryMin is null || c.SalaryMax is null || c.SalaryMin <= c.SalaryMax).WithErrorCode("VAL.SalaryRange.Invalid");
        RuleForEach(c => c.WorkArrangements).Must(w => Enum.TryParse<WorkArrangement>(w, true, out _)).WithErrorCode("VAL.WorkArrangement.Invalid");
    }
}

internal sealed class ConfigureJobPreferenceHandler(IJobPreferenceRepository repository, IProfileRepository profiles, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ConfigureJobPreferenceCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ConfigureJobPreferenceCommand request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var preference = await repository.GetByProfileAsync(profile.Id, ct);
        var isNew = preference is null;
        preference ??= JobPreference.CreateEmpty(profile.Id);
        preference.EnsureOwnedBy(new Actor(user.UserId!.Value), profile.OwnerAccountId);
        preference.Set(request.JobTypes, request.Industries, request.Locations, new SalaryRange(request.SalaryMin, request.SalaryMax, request.SalaryCurrency),
            request.WorkArrangements.Select(w => Enum.Parse<WorkArrangement>(w, true)).ToList(), clock.GetUtcNow().UtcDateTime);
        if (isNew)
        {
            repository.Add(preference);
        }

        return Result.Success();
    }
}

internal sealed class GetJobPreferenceHandler(IJobPreferenceRepository repository, IProfileRepository profiles, ICurrentUser user)
    : IQueryHandler<GetJobPreferenceQuery, JobPreferenceView>
{
    public async Task<Result<JobPreferenceView>> Handle(GetJobPreferenceQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var preference = await repository.GetByProfileAsync(profile.Id, ct);
        if (preference is null)
        {
            return new JobPreferenceView(profile.Id, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, Array.Empty<string>(),
                DateTime.MinValue);
        }

        return new JobPreferenceView(preference.ProfileId, preference.JobTypes, preference.Industries, preference.Locations,
            preference.SalaryExpectation is null ? null : new SalaryRangeView(preference.SalaryExpectation.Min, preference.SalaryExpectation.Max,
                preference.SalaryExpectation.Currency), preference.WorkArrangements.Select(w => w.ToString()).ToList(), preference.UpdatedAtUtc);
    }
}
