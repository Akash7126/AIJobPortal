using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class UpdateHelpContentHandler : ICommandHandler<UpdateHelpContentCommand, HelpContentView>
{
    private readonly IHelpContentRepository _content;
    private readonly IHelpOrganizationRepository _organizations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdateHelpContentHandler(IHelpContentRepository content, IHelpOrganizationRepository organizations, ICurrentUser user, TimeProvider clock)
    {
        _content = content;
        _organizations = organizations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<HelpContentView>> Handle(UpdateHelpContentCommand request, CancellationToken ct)
    {
        var content = await _content.GetByIdAsync(request.HelpContentId, ct);
        if (content is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        content.Update(new LocalizedText(request.TitleAr, request.TitleEn), new LocalizedText(request.BodyAr, request.BodyEn), ActorFactory.From(_user),
            _clock.GetUtcNow().UtcDateTime);
        var organization = await _organizations.GetAsync(content.Id, ct);
        return CreateHelpContentHandler.ToView(content, organization?.TopicId, organization?.Roles);
    }
}
