using FluentValidation;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application;

public sealed record SubmitHelpFeedbackCommand(Guid HelpContentId, FeedbackRating Rating, string? Comment) : AuthenticatedCommand<Unit>;

public sealed class SubmitHelpFeedbackValidator : AbstractValidator<SubmitHelpFeedbackCommand>
{
    public SubmitHelpFeedbackValidator()
    {
        RuleFor(c => c.Rating).IsInEnum().WithErrorCode("VAL.Rating.Invalid");
        RuleFor(c => c.Comment).MaximumLength(HelpFeedback.MaxCommentLength).WithErrorCode("VAL.Comment.TooLong");
    }
}

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

/// <summary>Story AC-03/AC-04: aggregate feedback is administrator-only.</summary>
public sealed record GetHelpFeedbackSummaryQuery(Guid HelpContentId) : AdminQuery<HelpFeedbackSummaryView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}

internal sealed class GetHelpFeedbackSummaryHandler : IQueryHandler<GetHelpFeedbackSummaryQuery, HelpFeedbackSummaryView>
{
    private readonly IHelpContentReadStore _store;

    public GetHelpFeedbackSummaryHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<HelpFeedbackSummaryView>> Handle(GetHelpFeedbackSummaryQuery request, CancellationToken ct) =>
        await _store.GetFeedbackSummaryAsync(request.HelpContentId, ct) ?? new HelpFeedbackSummaryView(request.HelpContentId, 0, 0);
}
