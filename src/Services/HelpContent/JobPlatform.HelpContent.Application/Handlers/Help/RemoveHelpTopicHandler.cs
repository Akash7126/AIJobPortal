using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class RemoveHelpTopicHandler : ICommandHandler<RemoveHelpTopicCommand, Unit>
{
    private readonly IHelpTopicRepository _topics;
    private readonly ICurrentUser _user;

    public RemoveHelpTopicHandler(IHelpTopicRepository topics, ICurrentUser user)
    {
        _topics = topics;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(RemoveHelpTopicCommand request, CancellationToken ct)
    {
        var topic = await _topics.GetByIdAsync(request.TopicId, ct);
        if (topic is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The topic was not found.");
        }

        topic.Remove(ActorFactory.From(_user));
        return Result.Success();
    }
}
