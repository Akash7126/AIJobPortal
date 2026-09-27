using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.EmployerOnboarding.Application;

/// <summary>Sets the "Verified Employer" badge flag once BC-01 approves the employer's government verification. Idempotent and monotonic:
/// a duplicate delivery or an older/out-of-order event (including one that arrives before the registration exists) never regresses the flag.</summary>
public sealed class MarkEmployerVerifiedHandler : IIntegrationEventHandler<EmployerVerificationApprovedIntegrationEvent>
{
    private readonly IEmployerStandingRepository _standings;
    private readonly IEmployerCache _cache;
    private readonly TimeProvider _clock;

    public MarkEmployerVerifiedHandler(IEmployerStandingRepository standings, IEmployerCache cache, TimeProvider clock)
    {
        _standings = standings;
        _cache = cache;
        _clock = clock;
    }

    public async Task Handle(EmployerVerificationApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var standing = await _standings.GetAsync(integrationEvent.EmployerAccountId, ct);
        if (standing is null)
        {
            // The registration/standing has not been opened yet (AccountApproved has not been processed, or never will be for this BC).
            // Open one so the badge is not lost; EmployerRegistration itself is opened independently by OpenEmployerRegistrationHandler.
            standing = EmployerStanding.OpenFor(Guid.NewGuid(), integrationEvent.EmployerAccountId);
            _standings.Add(standing);
        }

        standing.MarkVerified(integrationEvent.MessageId, integrationEvent.AggregateVersion, _clock.GetUtcNow().UtcDateTime);
        await _cache.InvalidateAsync(integrationEvent.EmployerAccountId, ct);
    }
}

// ---------------------------------------------------------------------- internal queries (foundation section 9.5: /internal/v1)

public sealed record GetEmployerStandingQuery(Guid EmployerAccountId) : ServiceQuery<EmployerStandingView>;

internal sealed class GetEmployerStandingHandler : IQueryHandler<GetEmployerStandingQuery, EmployerStandingView>
{
    private readonly IEmployerCache _cache;
    private readonly IEmployerReadStore _store;

    public GetEmployerStandingHandler(IEmployerCache cache, IEmployerReadStore store)
    {
        _cache = cache;
        _store = store;
    }

    public async Task<Result<EmployerStandingView>> Handle(GetEmployerStandingQuery request, CancellationToken ct)
    {
        if (await _cache.GetStandingAsync(request.EmployerAccountId, ct) is { } cached)
        {
            return cached;
        }

        var view = await _store.GetStandingAsync(request.EmployerAccountId, ct);
        if (view is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer was not found.");
        }

        await _cache.SetStandingAsync(request.EmployerAccountId, view, ct);
        return view;
    }
}

public sealed record GetCompanyPublicInfoQuery(Guid EmployerAccountId) : ServiceQuery<CompanyPublicInfoView>;

internal sealed class GetCompanyPublicInfoHandler : IQueryHandler<GetCompanyPublicInfoQuery, CompanyPublicInfoView>
{
    private readonly IEmployerCache _cache;
    private readonly IEmployerReadStore _store;

    public GetCompanyPublicInfoHandler(IEmployerCache cache, IEmployerReadStore store)
    {
        _cache = cache;
        _store = store;
    }

    public async Task<Result<CompanyPublicInfoView>> Handle(GetCompanyPublicInfoQuery request, CancellationToken ct)
    {
        if (await _cache.GetCompanyAsync(request.EmployerAccountId, ct) is { } cached)
        {
            return cached;
        }

        var view = await _store.GetCompanyPublicInfoAsync(request.EmployerAccountId, ct);
        if (view is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer was not found.");
        }

        await _cache.SetCompanyAsync(request.EmployerAccountId, view, ct);
        return view;
    }
}
