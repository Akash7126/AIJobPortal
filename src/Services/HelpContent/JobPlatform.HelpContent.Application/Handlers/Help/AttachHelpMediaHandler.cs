using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class AttachHelpMediaHandler : ICommandHandler<AttachHelpMediaCommand, HelpMediaView>
{
    private readonly IHelpContentRepository _content;
    private readonly IMediaStorage _storage;
    private readonly ICurrentUser _user;

    public AttachHelpMediaHandler(IHelpContentRepository content, IMediaStorage storage, ICurrentUser user)
    {
        _content = content;
        _storage = storage;
        _user = user;
    }

    public async Task<Result<HelpMediaView>> Handle(AttachHelpMediaCommand request, CancellationToken ct)
    {
        var content = await _content.GetByIdAsync(request.HelpContentId, ct);
        if (content is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        await using var stream = new MemoryStream(request.Content);
        var storageKey = await _storage.SaveAsync(stream, request.FileName, request.ContentType, ct);
        var media = content.AttachMedia(Guid.NewGuid(), request.Type, storageKey, request.CaptionsRef, request.TextAlternative, ActorFactory.From(_user));
        return new HelpMediaView(media.Id, media.Type.ToString(), _storage.UrlFor(storageKey), media.CaptionsRef, media.TextAlternative);
    }
}
