using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.Reporting.Application;

/// <summary>
/// Applies the role to report-category rules to the caller (roles come from the token's role claims, resolved per request, so a rule change applies without
/// re-login - AC-04) and records every decision (AC-03). The decision is persisted immediately, also for queries, which have no unit of work of their own.
/// </summary>
internal sealed class ReportAccessGuard : IReportAccessGuard
{
    private readonly IReportAccessRuleRepository _rules;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ReportAccessGuard(IReportAccessRuleRepository rules, ICurrentUser user, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _rules = rules;
        _user = user;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Unit>> EnsureAsync(ReportCategory category, string requestName, CancellationToken ct = default)
    {
        var roles = _user.RoleIds.Select(r => r.ToString()).Append(ReportAccessRule.AdministratorRole).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var callerRules = (await _rules.ListAsync(ct)).Where(r => roles.Contains(r.Role)).ToList();
        var allowed = ReportAccessPolicy.IsAllowed(callerRules, category);

        _rules.AddDecision(ReportAccessDecision.Record(_user.UserId ?? Guid.Empty, category, allowed, requestName, _clock.GetUtcNow().UtcDateTime));
        await _unitOfWork.SaveChangesAsync(ct);

        return allowed
            ? Result.Success()
            : Error.Forbidden(CategoryCodes.ForbiddenFor(category), "Your role may not access this report category.");
    }
}

/// <summary>HMAC-SHA256 signed, expiring links to generated reports (the distribution event carries the link, never the content).</summary>
public sealed class HmacReportLinkSigner : IReportLinkSigner
{
    private readonly byte[] _key;

    public HmacReportLinkSigner(IOptions<ReportingOptions> options) => _key = Encoding.UTF8.GetBytes(options.Value.LinkSigningKey);

    public string Sign(Guid exportId, DateTime expiresUtc) => Compute(exportId, new DateTimeOffset(expiresUtc, TimeSpan.Zero).ToUnixTimeSeconds());

    public bool Verify(Guid exportId, long expiresUnix, string signature, DateTime nowUtc)
    {
        if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime < nowUtc)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Compute(exportId, expiresUnix));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(signature ?? string.Empty));
    }

    private string Compute(Guid exportId, long expiresUnix) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{exportId:N}|{expiresUnix}"))).ToLowerInvariant();
}

public sealed record DateRange(DateOnly From, DateOnly To);

public static class DateRanges
{
    public const int MaxMonths = 24;

    public static readonly string[] Granularities = { "day", "week", "month" };

    /// <summary>Default window: the 30 days ending today.</summary>
    public static DateRange Resolve(DateOnly? from, DateOnly? to, TimeProvider clock)
    {
        var end = to ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return new DateRange(from ?? end.AddDays(-30), end);
    }

    public static DateTime StartUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>Exclusive end (start of the day after <paramref name="day"/>).</summary>
    public static DateTime EndUtc(DateOnly day) => day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
