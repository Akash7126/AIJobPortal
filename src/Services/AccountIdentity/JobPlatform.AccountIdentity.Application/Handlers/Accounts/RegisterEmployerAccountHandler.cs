using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class RegisterEmployerAccountHandler : ICommandHandler<RegisterEmployerAccountCommand, RegisteredAccountDto>
{
    private readonly RegistrationWorkflow _workflow;

    public RegisterEmployerAccountHandler(RegistrationWorkflow workflow) => _workflow = workflow;

    public Task<Result<RegisteredAccountDto>> Handle(RegisterEmployerAccountCommand r, CancellationToken ct) =>
        _workflow.RegisterAsync(new RegistrationInput(ActorType.Employer, r.CompanyName, r.Mobile, r.Email, r.CompanyId, r.RegistrationNumber,
            r.Level == 2 ? RegistrationLevel.Level2 : RegistrationLevel.Level1, r.Password, null), ct);
}
