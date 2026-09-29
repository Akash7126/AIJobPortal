using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

internal sealed class VerifyEmailHandler : ICommandHandler<VerifyEmailCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IApiSecretHasher _tokenHasher;
    private readonly TimeProvider _clock;

    public VerifyEmailHandler(IAccountRepository accounts, IApiSecretHasher tokenHasher, TimeProvider clock)
    {
        _accounts = accounts;
        _tokenHasher = tokenHasher;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        account.VerifyEmail(request.Token, _tokenHasher, _clock);
        return Result.Success();
    }
}
