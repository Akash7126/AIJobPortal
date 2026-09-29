using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class RevokePermissionHandler : ICommandHandler<RevokePermissionCommand, Unit>
{
    private readonly IRoleRepository _roles;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RevokePermissionHandler(IRoleRepository roles, ICurrentUser user, TimeProvider clock)
    {
        _roles = roles;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RevokePermissionCommand request, CancellationToken ct)
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

        role.RevokePermission(Actor.Administrator(_user.UserId!.Value), request.Permission, _clock);
        return Result.Success();
    }
}
