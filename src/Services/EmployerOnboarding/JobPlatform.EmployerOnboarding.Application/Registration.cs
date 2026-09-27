using FluentValidation;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.EmployerOnboarding.Application;

// ---------------------------------------------------------------------- commands

public sealed record SubmitEmployerLevel2Command(
    string CompanyName, string CompanyId, string RegistrationNumber,
    string Website, string Industry, CompanySize Size, string Governorate, string City, string? Street, string Description)
    : EmployerCommand<EmployerRegistrationView>;

public sealed class SubmitEmployerLevel2Validator : AbstractValidator<SubmitEmployerLevel2Command>
{
    public SubmitEmployerLevel2Validator()
    {
        RuleFor(c => c.CompanyName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.CompanyName.Required");
        RuleFor(c => c.CompanyId).NotEmpty().MaximumLength(100).WithErrorCode("VAL.CompanyId.Required");
        RuleFor(c => c.RegistrationNumber).NotEmpty().MaximumLength(100).WithErrorCode("VAL.RegistrationNumber.Required");
        RuleFor(c => c.Website).NotEmpty().Must(BeAnAbsoluteHttpUrl).WithErrorCode("VAL.Website.Invalid");
        RuleFor(c => c.Industry).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Industry.Required");
        RuleFor(c => c.Size).IsInEnum().WithErrorCode("VAL.Size.Invalid");
        RuleFor(c => c.Governorate).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Governorate.Required");
        RuleFor(c => c.City).NotEmpty().MaximumLength(100).WithErrorCode("VAL.City.Required");
        RuleFor(c => c.Description).NotEmpty().MaximumLength(2000).WithErrorCode("VAL.Description.TooLong");
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

internal sealed class SubmitEmployerLevel2Handler : ICommandHandler<SubmitEmployerLevel2Command, EmployerRegistrationView>
{
    private readonly IEmployerRegistrationRepository _registrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SubmitEmployerLevel2Handler(IEmployerRegistrationRepository registrations, ICurrentUser user, TimeProvider clock)
    {
        _registrations = registrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<EmployerRegistrationView>> Handle(SubmitEmployerLevel2Command request, CancellationToken ct)
    {
        var employerAccountId = _user.UserId!.Value;
        var registration = await _registrations.GetByEmployerAsync(employerAccountId, ct);
        if (registration is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No pending registration was found for this employer account.");
        }

        var identity = new CompanyIdentity(request.CompanyName, request.CompanyId, request.RegistrationNumber);
        var level2 = new Level2Details(request.Website, request.Industry, request.Size, new Address(request.Governorate, request.City, request.Street),
            request.Description);
        registration.SubmitLevel2(identity, level2, ActorFactory.From(_user));
        return ToView(registration);
    }

    internal static EmployerRegistrationView ToView(EmployerRegistration r) => new(
        r.Id, r.EmployerAccountId, r.Status.ToString(),
        r.CompanyIdentity is { } identity ? new CompanyIdentityView(identity.Name, identity.CompanyId, identity.RegistrationNumber) : null,
        r.Level2 is { } level2 ? new Level2View(level2.Website, level2.Industry, level2.Size.ToString(), level2.Address.Governorate, level2.Address.City,
            level2.Address.Street, level2.Description) : null,
        r.OpenedAtUtc, r.ApprovedBy, r.ApprovedAtUtc, r.RowVersion);
}

public sealed record ApproveEmployerRegistrationCommand(Guid EmployerRegistrationId) : AdminCommand<Unit>;

public sealed class ApproveEmployerRegistrationValidator : AbstractValidator<ApproveEmployerRegistrationCommand>
{
    public ApproveEmployerRegistrationValidator() => RuleFor(c => c.EmployerRegistrationId).NotEmpty().WithErrorCode("VAL.EmployerRegistrationId.Required");
}

internal sealed class ApproveEmployerRegistrationHandler : ICommandHandler<ApproveEmployerRegistrationCommand, Unit>
{
    private readonly IEmployerRegistrationRepository _registrations;
    private readonly IEmployerStandingRepository _standings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ApproveEmployerRegistrationHandler(IEmployerRegistrationRepository registrations, IEmployerStandingRepository standings, ICurrentUser user,
        TimeProvider clock)
    {
        _registrations = registrations;
        _standings = standings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ApproveEmployerRegistrationCommand request, CancellationToken ct)
    {
        var registration = await _registrations.GetByIdAsync(request.EmployerRegistrationId, ct);
        if (registration is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer registration was not found.");
        }

        registration.Approve(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);

        var standing = await _standings.GetAsync(registration.EmployerAccountId, ct);
        if (standing is null)
        {
            standing = EmployerStanding.OpenFor(Guid.NewGuid(), registration.EmployerAccountId);
            _standings.Add(standing);
        }

        standing.MarkAdmissionApproved(_clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- queries

public sealed record GetEmployerRegistrationQuery : EmployerQuery<EmployerRegistrationView>;

internal sealed class GetEmployerRegistrationHandler : IQueryHandler<GetEmployerRegistrationQuery, EmployerRegistrationView>
{
    private readonly IEmployerReadStore _store;
    private readonly ICurrentUser _user;

    public GetEmployerRegistrationHandler(IEmployerReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<EmployerRegistrationView>> Handle(GetEmployerRegistrationQuery request, CancellationToken ct) =>
        await _store.GetRegistrationAsync(_user.UserId!.Value, ct) is { } view
            ? view
            : Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No registration was found for this employer account.");
}

public sealed record ListEmployerRegistrationsQuery(string? Status, int Page = 1, int PageSize = 20) : AdminQuery<PagedResult<EmployerRegistrationView>>;

internal sealed class ListEmployerRegistrationsHandler : IQueryHandler<ListEmployerRegistrationsQuery, PagedResult<EmployerRegistrationView>>
{
    private readonly IEmployerReadStore _store;

    public ListEmployerRegistrationsHandler(IEmployerReadStore store) => _store = store;

    public async Task<Result<PagedResult<EmployerRegistrationView>>> Handle(ListEmployerRegistrationsQuery request, CancellationToken ct) =>
        await _store.ListRegistrationsAsync(request.Status, new PageRequest(request.Page, request.PageSize), ct);
}

// ---------------------------------------------------------------------- inbox

/// <summary>Opens a Pending registration and a standing row when BC-03 approves an employer account. Idempotent per employer account
/// (a redelivery, or a second AccountApproved for the same account, changes nothing).</summary>
public sealed class OpenEmployerRegistrationHandler : IIntegrationEventHandler<AccountApprovedIntegrationEvent>
{
    private readonly IEmployerRegistrationRepository _registrations;
    private readonly IEmployerStandingRepository _standings;
    private readonly IKnownAccountRepository _knownAccounts;
    private readonly TimeProvider _clock;

    public OpenEmployerRegistrationHandler(IEmployerRegistrationRepository registrations, IEmployerStandingRepository standings,
        IKnownAccountRepository knownAccounts, TimeProvider clock)
    {
        _registrations = registrations;
        _standings = standings;
        _knownAccounts = knownAccounts;
        _clock = clock;
    }

    public async Task Handle(AccountApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.ActorType != ActorType.Employer)
        {
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (await _knownAccounts.GetAsync(integrationEvent.AccountId, ct) is null)
        {
            _knownAccounts.Add(new KnownAccount(integrationEvent.AccountId, now));
        }

        if (await _registrations.GetByEmployerAsync(integrationEvent.AccountId, ct) is null)
        {
            _registrations.Add(EmployerRegistration.OpenFor(Guid.NewGuid(), integrationEvent.AccountId, now));
        }

        if (await _standings.GetAsync(integrationEvent.AccountId, ct) is null)
        {
            _standings.Add(EmployerStanding.OpenFor(Guid.NewGuid(), integrationEvent.AccountId));
        }
    }
}
