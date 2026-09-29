using JobPlatform.HelpContent.Application.Commands.Feedback;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Feedback;

/// <summary>INV-12: one feedback per (user, article) - a repeat replaces the prior rating rather than duplicating (story AC-02).
/// INV-13: the referenced article must exist.</summary>
internal sealed class SubmitHelpFeedbackHandler : ICommandHandler<SubmitHelpFeedbackCommand, Unit>
{
    private readonly IHelpFeedbackRepository _feedback;
    private readonly IHelpContentRepository _content;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SubmitHelpFeedbackHandler(IHelpFeedbackRepository feedback, IHelpContentRepository content, ICurrentUser user, TimeProvider clock)
    {
        _feedback = feedback;
        _content = content;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(SubmitHelpFeedbackCommand request, CancellationToken ct)
    {
        if (await _content.GetByIdAsync(request.HelpContentId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The help content was not found.");
        }

        var userId = _user.UserId!.Value;
        var now = _clock.GetUtcNow().UtcDateTime;
        var existing = await _feedback.GetByUserAndContentAsync(userId, request.HelpContentId, ct);
        if (existing is null)
        {
            _feedback.Add(HelpFeedback.Submit(Guid.NewGuid(), request.HelpContentId, userId, request.Rating, request.Comment, now));
        }
        else
        {
            existing.Replace(request.Rating, request.Comment, now);
        }

        return Result.Success();
    }
}
