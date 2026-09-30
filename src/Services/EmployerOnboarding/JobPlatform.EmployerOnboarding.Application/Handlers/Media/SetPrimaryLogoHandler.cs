using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Media;

internal sealed class SetPrimaryLogoHandler : ICommandHandler<SetPrimaryLogoCommand, Unit>
{
    private readonly ICompanyMediaRepository _media;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SetPrimaryLogoHandler(ICompanyMediaRepository media, ICurrentUser user, TimeProvider clock)
    {
        _media = media;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(SetPrimaryLogoCommand request, CancellationToken ct)
    {
        var target = await _media.GetByIdAsync(request.CompanyMediaId, ct);
        if (target is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The media item was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var actor = ActorFactory.From(_user);
        foreach (var other in await _media.ListByEmployerAsync(target.EmployerAccountId, ct))
        {
            if (other.IsPrimaryLogo && other.Id != target.Id)
            {
                other.UnsetPrimaryLogo();
            }
        }

        target.SetAsPrimaryLogo(actor, now);
        return Result.Success();
    }
}
