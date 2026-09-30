using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class ConfigurePasswordPolicyHandler : ICommandHandler<ConfigurePasswordPolicyCommand, Unit>
{
    private readonly IPasswordPolicyRepository _policies;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ConfigurePasswordPolicyHandler(IPasswordPolicyRepository policies, ICurrentUser user, TimeProvider clock)
    {
        _policies = policies;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ConfigurePasswordPolicyCommand request, CancellationToken ct)
    {
        var policy = await _policies.GetAsync(ct);
        if (!ETag.Matches(request.IfMatch, policy.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        policy.Configure(Actor.Administrator(_user.UserId!.Value), request.MinLength, request.RequireUpper, request.RequireLower, request.RequireDigit, _clock);
        return Result.Success();
    }
}
