using FluentValidation;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Offerings;

/// <summary>US-3.1.4-09: suspend a job offering (hidden from search; BC-09 enforces it on JobOfferingSuspended).</summary>
public sealed record SuspendJobOfferingCommand(Guid JobOfferingId, string? Reason) : AdminCommand<Guid>;

/// <summary>Proposed (Q-04): remove a job offering. Same guards and event as a suspension, recorded as removed.</summary>
public sealed record RemoveJobOfferingCommand(Guid JobOfferingId, string? Reason) : AdminCommand<Guid>;

public static class ModerationRules
{
    public const int MinReasonLength = 5;
    public const int MaxReasonLength = 500;
}

public sealed class SuspendJobOfferingValidator : AbstractValidator<SuspendJobOfferingCommand>
{
    public SuspendJobOfferingValidator()
    {
        RuleFor(c => c.JobOfferingId).NotEmpty().WithErrorCode("VAL.JobOfferingId.Required");
        RuleFor(c => c.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required")
            .Must(r => r is null || r.Trim().Length is >= ModerationRules.MinReasonLength and <= ModerationRules.MaxReasonLength).WithErrorCode("VAL.Reason.OutOfRange");
    }
}

public sealed class RemoveJobOfferingValidator : AbstractValidator<RemoveJobOfferingCommand>
{
    public RemoveJobOfferingValidator()
    {
        RuleFor(c => c.JobOfferingId).NotEmpty().WithErrorCode("VAL.JobOfferingId.Required");
        RuleFor(c => c.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required")
            .Must(r => r is null || r.Trim().Length is >= ModerationRules.MinReasonLength and <= ModerationRules.MaxReasonLength).WithErrorCode("VAL.Reason.OutOfRange");
    }
}

internal sealed class SuspendJobOfferingHandler : ICommandHandler<SuspendJobOfferingCommand, Guid>
{
    private readonly IJobOfferingRepository _offerings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SuspendJobOfferingHandler(IJobOfferingRepository offerings, ICurrentUser user, TimeProvider clock)
    {
        _offerings = offerings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(SuspendJobOfferingCommand request, CancellationToken ct)
    {
        var offering = await _offerings.GetByIdAsync(request.JobOfferingId, ct);
        if (offering is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job offering was not found.");
        }

        offering.Suspend(ActorFactory.From(_user), request.Reason, _clock.GetUtcNow().UtcDateTime);
        return offering.Id;
    }
}

internal sealed class RemoveJobOfferingHandler : ICommandHandler<RemoveJobOfferingCommand, Guid>
{
    private readonly IJobOfferingRepository _offerings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RemoveJobOfferingHandler(IJobOfferingRepository offerings, ICurrentUser user, TimeProvider clock)
    {
        _offerings = offerings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RemoveJobOfferingCommand request, CancellationToken ct)
    {
        var offering = await _offerings.GetByIdAsync(request.JobOfferingId, ct);
        if (offering is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job offering was not found.");
        }

        offering.Remove(ActorFactory.From(_user), request.Reason, _clock.GetUtcNow().UtcDateTime);
        return offering.Id;
    }
}

public sealed record ListJobOfferingsQuery(string? Status, int Page = 1, int PageSize = 20) : AdminQuery<PagedResult<JobOfferingListItem>>;

public sealed class ListJobOfferingsValidator : AbstractValidator<ListJobOfferingsQuery>
{
    public ListJobOfferingsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
        RuleFor(q => q.Status).Must(s => Enum.TryParse<JobOfferingStatus>(s, true, out var v) && Enum.IsDefined(v)).WithErrorCode("VAL.Status.Invalid")
            .When(q => q.Status is not null);
    }
}

internal sealed class ListJobOfferingsHandler : IQueryHandler<ListJobOfferingsQuery, PagedResult<JobOfferingListItem>>
{
    private readonly IAdminReadStore _store;

    public ListJobOfferingsHandler(IAdminReadStore store) => _store = store;

    public async Task<Result<PagedResult<JobOfferingListItem>>> Handle(ListJobOfferingsQuery request, CancellationToken ct) =>
        await _store.ListJobOfferingsAsync(request.Status, new PageRequest(request.Page, request.PageSize), ct);
}
