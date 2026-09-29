using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class GrantPermissionHandler : ICommandHandler<GrantPermissionCommand, Unit>
{
    private readonly IRoleRepository _roles;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public GrantPermissionHandler(IRoleRepository roles, ICurrentUser user, TimeProvider clock)
    {
        _roles = roles;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(GrantPermissionCommand request, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(new RoleId(request.RoleId), ct);
        if (role is null)
        {
            return RoleErrors.RoleNotFound;
        }

        if (!ETag.Matches(request.IfMatch, role.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        role.GrantPermission(Actor.Administrator(_user.UserId!.Value), request.Permission, _clock);
        return Result.Success();
    }
}
