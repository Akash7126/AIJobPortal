using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Media;

internal sealed class RemoveCompanyMediaHandler : ICommandHandler<RemoveCompanyMediaCommand, Unit>
{
    private readonly ICompanyMediaRepository _media;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RemoveCompanyMediaHandler(ICompanyMediaRepository media, ICurrentUser user, TimeProvider clock)
    {
        _media = media;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RemoveCompanyMediaCommand request, CancellationToken ct)
    {
        var media = await _media.GetByIdAsync(request.CompanyMediaId, ct);
        if (media is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The media item was not found.");
        }

        media.Remove(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
