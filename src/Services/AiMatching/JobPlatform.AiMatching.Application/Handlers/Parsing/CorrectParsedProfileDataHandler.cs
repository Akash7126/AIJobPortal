using JobPlatform.AiMatching.Application.Commands.Parsing;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Parsing;

internal sealed class CorrectParsedProfileDataHandler(IParsedProfileDataRepository repository, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CorrectParsedProfileDataCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CorrectParsedProfileDataCommand request, CancellationToken ct)
    {
        var data = user.UserId is { } owner ? await repository.GetByOwnerAsync(owner, ct) : null;
        if (data is null)
        {
            return Error.NotFound(AiErrorCodes.NotFound, "There is no parsed data to correct yet.");
        }

        data.Correct(Enum.Parse<ParsedFieldName>(request.Field, true), request.Value, user.ToActor(), clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
