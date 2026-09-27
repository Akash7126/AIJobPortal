using System.Globalization;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>
/// Five-field cron (minute hour day-of-month month day-of-week; UTC) supporting numbers, lists, ranges, steps and *. The minute field must be one value so a schedule
/// fires at most once an hour (handover 7: "min 1 h"). Pure and framework-free.
/// </summary>
public sealed class CronExpression
{
    private readonly int _minute;
    private readonly HashSet<int> _hours;
    private readonly HashSet<int> _days;
    private readonly HashSet<int> _months;
    private readonly HashSet<int> _weekdays;

    private CronExpression(string text, int minute, HashSet<int> hours, HashSet<int> days, HashSet<int> months, HashSet<int> weekdays)
    {
        Text = text;
        _minute = minute;
        _hours = hours;
        _days = days;
        _months = months;
        _weekdays = weekdays;
    }

    public string Text { get; }

    public static bool TryParse(string? text, out CronExpression? cron)
    {
        cron = null;
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 5 })
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minute) || minute is < 0 or > 59)
        {
            return false;
        }

        var hours = Expand(parts[1], 0, 23);
        var days = Expand(parts[2], 1, 31);
        var months = Expand(parts[3], 1, 12);
        var weekdays = Expand(parts[4], 0, 7);
        if (hours is null || days is null || months is null || weekdays is null)
        {
            return false;
        }

        if (weekdays.Remove(7))
        {
            weekdays.Add(0);
        }

        cron = new CronExpression(string.Join(' ', parts), minute, hours, days, months, weekdays);
        return true;
    }

    /// <summary>The first matching instant strictly after <paramref name="afterUtc"/> (within four years), or null when none exists.</summary>
    public DateTime? NextAfter(DateTime afterUtc)
    {
        var candidate = new DateTime(afterUtc.Year, afterUtc.Month, afterUtc.Day, afterUtc.Hour, _minute, 0, DateTimeKind.Utc);
        if (candidate <= afterUtc)
        {
            candidate = candidate.AddHours(1);
        }

        var limit = afterUtc.AddYears(4);
        while (candidate <= limit)
        {
            if (_months.Contains(candidate.Month) && _days.Contains(candidate.Day) && _weekdays.Contains((int)candidate.DayOfWeek) && _hours.Contains(candidate.Hour))
            {
                return candidate;
            }

            candidate = candidate.AddHours(1);
        }

        return null;
    }

    private static HashSet<int>? Expand(string field, int min, int max)
    {
        var result = new HashSet<int>();
        foreach (var part in field.Split(','))
        {
            var stepSplit = part.Split('/');
            var step = 1;
            if (stepSplit.Length > 2 || (stepSplit.Length == 2 && (!int.TryParse(stepSplit[1], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step < 1)))
            {
                return null;
            }

            int lo;
            int hi;
            if (stepSplit[0] == "*")
            {
                lo = min;
                hi = max;
            }
            else if (stepSplit[0].Contains('-'))
            {
                var range = stepSplit[0].Split('-');
                if (range.Length != 2 || !int.TryParse(range[0], NumberStyles.None, CultureInfo.InvariantCulture, out lo) || !int.TryParse(range[1], NumberStyles.None, CultureInfo.InvariantCulture, out hi))
                {
                    return null;
                }
            }
            else
            {
                if (!int.TryParse(stepSplit[0], NumberStyles.None, CultureInfo.InvariantCulture, out lo))
                {
                    return null;
                }

                hi = stepSplit.Length == 2 ? max : lo;
            }

            if (lo < min || hi > max || lo > hi)
            {
                return null;
            }

            for (var v = lo; v <= hi; v += step)
            {
                result.Add(v);
            }
        }

        return result.Count == 0 ? null : result;
    }
}

public sealed record ReportDistributionRequestedDomainEvent(DateTime At, Guid ScheduleId, string ReportRef, IReadOnlyList<string> Recipients, string Format) : DomainEvent(At);

/// <summary>
/// A recurring run of a template or saved report (US-3.5.4-04). INV-04: the interval must be valid (Daily by default - A-02-013 - Weekly, Monthly, or Cron with
/// at most one run an hour). On every run the generated report is distributed by e-mail through BC-13 (ReportDistributionRequested, AC-04).
/// </summary>
public sealed class ReportSchedule : AggregateRoot<Guid>
{
    public const int MaxRecipients = 50;

    private ReportSchedule()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public Guid? TemplateId { get; private set; }
    public Guid? SavedReportId { get; private set; }
    public ScheduleInterval Interval { get; private set; }
    public string? CronText { get; private set; }
    public string RecipientsCsv { get; private set; } = string.Empty;
    public ReportFormat Format { get; private set; }
    public DateTime NextRunAtUtc { get; private set; }
    public DateTime? LastRunAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedBy { get; private set; }

    public IReadOnlyList<string> Recipients => RecipientsCsv.Length == 0 ? Array.Empty<string>() : RecipientsCsv.Split(';');

    public static ReportSchedule Create(string name, Guid? templateId, Guid? savedReportId, ScheduleInterval? interval, string? cron, IReadOnlyList<string> recipients,
        ReportFormat format, Guid administratorId, DateTime nowUtc)
    {
        var schedule = new ReportSchedule { Id = Guid.NewGuid(), CreatedBy = administratorId, IsActive = true };
        schedule.Reconfigure(name, templateId, savedReportId, interval, cron, recipients, format, nowUtc);
        return schedule;
    }

    public void Reconfigure(string name, Guid? templateId, Guid? savedReportId, ScheduleInterval? interval, string? cron, IReadOnlyList<string> recipients, ReportFormat format,
        DateTime nowUtc)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(name) && name.Length <= 150, ReportingRuleCodes.InvalidScheduleInterval, "A schedule needs a name of up to 150 characters.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure((templateId is null) != (savedReportId is null), ReportingRuleCodes.InvalidScheduleInterval, "A schedule targets exactly one template or saved report.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        var effective = interval ?? ScheduleInterval.Daily;
        Guard.Ensure(Enum.IsDefined(effective), ReportingRuleCodes.InvalidScheduleInterval, "Unknown schedule interval.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(Enum.IsDefined(format), ReportingRuleCodes.InvalidScheduleInterval, "Unknown report format.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(recipients.Count is >= 1 and <= MaxRecipients, ReportingRuleCodes.InvalidScheduleInterval, $"A schedule needs between 1 and {MaxRecipients} recipients.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);

        CronExpression? parsed = null;
        if (effective == ScheduleInterval.Cron)
        {
            Guard.Ensure(CronExpression.TryParse(cron, out parsed) && parsed!.NextAfter(nowUtc) is not null, ReportingRuleCodes.InvalidScheduleInterval,
                "The cron expression is invalid, or fires more than once an hour.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        }

        Name = name.Trim();
        TemplateId = templateId;
        SavedReportId = savedReportId;
        Interval = effective;
        CronText = parsed?.Text;
        RecipientsCsv = string.Join(';', recipients.Select(r => r.Trim().ToLowerInvariant()).Distinct());
        Format = format;
        NextRunAtUtc = FirstRun(effective, parsed, nowUtc);
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public bool IsDue(DateTime nowUtc) => IsActive && NextRunAtUtc <= nowUtc;

    /// <summary>The run produced a report: request its distribution (signed link, never the content) and schedule the next run after <paramref name="nowUtc"/>.</summary>
    public void CompleteRun(string signedReportRef, DateTime nowUtc)
    {
        Guard.Ensure(IsDue(nowUtc), ReportingRuleCodes.InvalidScheduleInterval, "The schedule is not due.", kind: BusinessRuleKind.Conflict);
        Guard.Ensure(!string.IsNullOrWhiteSpace(signedReportRef), ReportingRuleCodes.InvalidScheduleInterval, "A report reference is required.");
        Raise(new ReportDistributionRequestedDomainEvent(nowUtc, Id, signedReportRef, Recipients, Format.ToString()));
        LastRunAtUtc = nowUtc;
        NextRunAtUtc = Advance(NextRunAtUtc, nowUtc);
    }

    /// <summary>The run failed: try again at the next occurrence without distributing anything.</summary>
    public void SkipRun(DateTime nowUtc) => NextRunAtUtc = Advance(NextRunAtUtc, nowUtc);

    private DateTime Advance(DateTime scheduled, DateTime nowUtc)
    {
        var next = scheduled;
        do
        {
            next = Next(next);
        }
        while (next <= nowUtc);
        return next;
    }

    private DateTime Next(DateTime from) => Interval switch
    {
        ScheduleInterval.Daily => from.AddDays(1),
        ScheduleInterval.Weekly => from.AddDays(7),
        ScheduleInterval.Monthly => from.AddMonths(1),
        _ => CronExpression.TryParse(CronText, out var cron) ? cron!.NextAfter(from) ?? from.AddYears(100) : from.AddYears(100)
    };

    private static DateTime FirstRun(ScheduleInterval interval, CronExpression? cron, DateTime nowUtc)
    {
        var midnight = nowUtc.Date.AddDays(1);
        return interval switch
        {
            ScheduleInterval.Daily => midnight,
            ScheduleInterval.Weekly => midnight.AddDays(((int)DayOfWeek.Monday - (int)midnight.DayOfWeek + 7) % 7),
            ScheduleInterval.Monthly => new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
            _ => cron!.NextAfter(nowUtc)!.Value
        };
    }
}
