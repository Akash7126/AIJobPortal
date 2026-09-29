using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.Validators.Integrations;

public sealed class ConfigureSyncScheduleValidator : AbstractValidator<ConfigureSyncScheduleCommand>
{
    public ConfigureSyncScheduleValidator()
    {
        RuleFor(c => c.Mode).Must(m => Enum.TryParse<SyncMode>(m, true, out _)).WithErrorCode("VAL.Mode.Invalid");
        When(c => Enum.TryParse<SyncMode>(c.Mode, true, out var mode) && mode == SyncMode.Scheduled, () =>
        {
            RuleFor(c => c.Cron).NotEmpty().WithErrorCode("VAL.Cron.Required");
            RuleFor(c => c.Cron).Must(HasAtLeastFiveMinuteInterval).When(c => !string.IsNullOrWhiteSpace(c.Cron))
                .WithErrorCode("VAL.Cron.MinimumIntervalFiveMinutes");
        });
    }

    /// <summary>Pragmatic cron validation (no cron library dependency): five space-separated fields, and the minute field must not
    /// fire more often than every 5 minutes (handover 3.4.1-05 AC-02).</summary>
    private static bool HasAtLeastFiveMinuteInterval(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return false;
        }

        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            return false;
        }

        var minute = parts[0];
        if (minute == "*")
        {
            return false;
        }

        return !minute.StartsWith("*/", StringComparison.Ordinal) || (int.TryParse(minute[2..], out var step) && step >= 5);
    }
}
