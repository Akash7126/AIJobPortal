using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.DTOs.Common;
using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class CreateHelpTopicHandler : ICommandHandler<CreateHelpTopicCommand, HelpTopicView>
{
    private readonly IHelpTopicRepository _topics;
    private readonly ICurrentUser _user;

    public CreateHelpTopicHandler(IHelpTopicRepository topics, ICurrentUser user)
    {
        _topics = topics;
        _user = user;
    }

    public async Task<Result<HelpTopicView>> Handle(CreateHelpTopicCommand request, CancellationToken ct)
    {
        var topic = HelpTopic.Create(Guid.NewGuid(), new LocalizedText(request.NameAr, request.NameEn), ActorFactory.From(_user));
        _topics.Add(topic);
        return await Task.FromResult(new HelpTopicView(topic.Id, new LocalizedView(topic.Name.Ar, topic.Name.En), topic.IsRemoved));
    }
}
