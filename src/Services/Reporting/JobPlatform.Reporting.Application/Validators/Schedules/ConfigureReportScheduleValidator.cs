using FluentValidation;
using JobPlatform.Reporting.Application.Commands.Schedules;
using JobPlatform.Reporting.Application.Validators.Common;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.Reporting.Application.Validators.Schedules;

/// <summary>ConfigureReportScheduleValidator (handover 7): valid interval, recipients are valid e-mails (at most 50), format known.</summary>
public sealed class ConfigureReportScheduleValidator : AbstractValidator<ConfigureReportScheduleCommand>
{
    public ConfigureReportScheduleValidator()
    {
        RuleFor(x => x.Name).ValidReportName();
        RuleFor(x => x.Interval).Must(i => i is null || Enum.TryParse<ScheduleInterval>(i, true, out _)).WithErrorCode("VAL.Interval.Unknown");
        RuleFor(x => x.Format).KnownReportFormat();
        RuleFor(x => x.Recipients).NotNull().WithErrorCode("VAL.Recipients.Required");
        RuleFor(x => x.Recipients).Must(r => r is null || r.Count is >= 1 and <= ReportSchedule.MaxRecipients).WithErrorCode("VAL.Recipients.Count");
        RuleForEach(x => x.Recipients).Must(r => Email.TryCreate(r, out _)).WithErrorCode("VAL.Recipient.InvalidEmail");
    }
}
