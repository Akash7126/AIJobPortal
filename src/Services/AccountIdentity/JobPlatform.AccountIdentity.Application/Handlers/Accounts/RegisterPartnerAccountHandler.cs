using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class RegisterPartnerAccountHandler : ICommandHandler<RegisterPartnerAccountCommand, RegisteredAccountDto>
{
    private readonly RegistrationWorkflow _workflow;

    public RegisterPartnerAccountHandler(RegistrationWorkflow workflow) => _workflow = workflow;

    public Task<Result<RegisteredAccountDto>> Handle(RegisterPartnerAccountCommand r, CancellationToken ct) =>
        _workflow.RegisterAsync(new RegistrationInput(ActorType.ExternalJobSite, r.OrganisationName, r.Mobile, r.ContactEmail, r.Identity, null,
            RegistrationLevel.Level1, r.Password, null), ct);
}
