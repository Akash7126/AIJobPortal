using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class SetContextHelpMappingHandler : ICommandHandler<SetContextHelpMappingCommand, Unit>
{
    private readonly IContextHelpMappingRepository _mappings;
    private readonly IHelpContentRepository _content;
    private readonly ICurrentUser _user;

    public SetContextHelpMappingHandler(IContextHelpMappingRepository mappings, IHelpContentRepository content, ICurrentUser user)
    {
        _mappings = mappings;
        _content = content;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(SetContextHelpMappingCommand request, CancellationToken ct)
    {
        if (await _content.GetByIdAsync(request.HelpContentId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        var actor = ActorFactory.From(_user);
        var mapping = await _mappings.GetByPageKeyAsync(request.PageKey, ct);
        if (mapping is null)
        {
            _mappings.Add(ContextHelpMapping.Create(Guid.NewGuid(), request.PageKey, request.HelpContentId, actor));
        }
        else
        {
            mapping.Repoint(request.HelpContentId, actor);
        }

        return Result.Success();
    }
}
