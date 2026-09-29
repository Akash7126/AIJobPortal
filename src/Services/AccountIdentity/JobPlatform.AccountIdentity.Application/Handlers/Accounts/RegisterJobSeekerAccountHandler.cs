using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class RegisterJobSeekerAccountHandler : ICommandHandler<RegisterJobSeekerAccountCommand, RegisteredAccountDto>
{
    private readonly RegistrationWorkflow _workflow;

    public RegisterJobSeekerAccountHandler(RegistrationWorkflow workflow) => _workflow = workflow;

    public Task<Result<RegisteredAccountDto>> Handle(RegisterJobSeekerAccountCommand r, CancellationToken ct) =>
        _workflow.RegisterAsync(new RegistrationInput(ActorType.JobSeeker, r.FullName, r.Mobile, r.Email, null, null, RegistrationLevel.Level1,
            r.Password, r.PreferredLanguage), ct);
}
