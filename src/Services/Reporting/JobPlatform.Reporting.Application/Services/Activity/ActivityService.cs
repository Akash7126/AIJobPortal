using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Services.Activity;

/// <summary>Logic shared by the activity request handlers.</summary>
internal sealed class ActivityService
{
    private readonly TimeProvider _clock;

    public ActivityService(TimeProvider clock) => _clock = clock;

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public static RetentionPolicyDto ToDto(ActivityLogRetentionPolicy p) => new(p.RetentionMonths, p.LegalMinimumMonths, p.UpdatedAtUtc);
}
