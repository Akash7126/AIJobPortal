using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class UpdateHelpContentOrganizationHandler : ICommandHandler<UpdateHelpContentOrganizationCommand, Unit>
{
    private readonly IHelpContentRepository _content;
    private readonly IHelpOrganizationRepository _organizations;
    private readonly ICurrentUser _user;

    public UpdateHelpContentOrganizationHandler(IHelpContentRepository content, IHelpOrganizationRepository organizations, ICurrentUser user)
    {
        _content = content;
        _organizations = organizations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(UpdateHelpContentOrganizationCommand request, CancellationToken ct)
    {
        if (await _content.GetByIdAsync(request.HelpContentId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        var organization = await _organizations.GetAsync(request.HelpContentId, ct);
        if (organization is null)
        {
            organization = HelpContentOrganization.CreateFor(request.HelpContentId);
            _organizations.Add(organization);
        }

        organization.AssignTopicAndRoles(request.TopicId, request.Roles, ActorFactory.From(_user));
        return Result.Success();
    }
}
