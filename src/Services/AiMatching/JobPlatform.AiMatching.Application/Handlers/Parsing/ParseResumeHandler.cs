using JobPlatform.AiMatching.Application.Commands.Parsing;
using JobPlatform.AiMatching.Application.Services.Parsing;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Parsing;

internal sealed class ParseResumeHandler(ResumeParsingService service) : ICommandHandler<ParseResumeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ParseResumeCommand request, CancellationToken ct)
    {
        await service.ParseAsync(request.ResumeId, request.ProfileId, request.Format, request.SizeBytes, request.Sha256, ct);
        return Result.Success();
    }
}
