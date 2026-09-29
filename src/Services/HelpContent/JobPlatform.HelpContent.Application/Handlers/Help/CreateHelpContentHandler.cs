using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.DTOs.Common;
using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class CreateHelpContentHandler : ICommandHandler<CreateHelpContentCommand, HelpContentView>
{
    private readonly IHelpContentRepository _content;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreateHelpContentHandler(IHelpContentRepository content, ICurrentUser user, TimeProvider clock)
    {
        _content = content;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<HelpContentView>> Handle(CreateHelpContentCommand request, CancellationToken ct)
    {
        var content = Domain.HelpContent.Create(Guid.NewGuid(), request.Kind, new LocalizedText(request.TitleAr, request.TitleEn),
            new LocalizedText(request.BodyAr, request.BodyEn), ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _content.Add(content);
        return await Task.FromResult(ToView(content));
    }

    internal static HelpContentView ToView(Domain.HelpContent c, Guid? topicId = null, IReadOnlyList<HelpRole>? roles = null) => new(
        c.Id, c.Kind.ToString(), new LocalizedView(c.Current.Title.Ar, c.Current.Title.En), new LocalizedView(c.Current.Body.Ar, c.Current.Body.En),
        c.CurrentVersion, topicId, (roles ?? Array.Empty<HelpRole>()).Select(r => r.ToString()).ToArray(),
        c.Media.Select(m => new HelpMediaView(m.Id, m.Type.ToString(), m.StorageKey, m.CaptionsRef, m.TextAlternative)).ToArray(), c.RowVersion);
}
