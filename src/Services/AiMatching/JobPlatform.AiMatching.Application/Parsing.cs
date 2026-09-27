using System.Diagnostics;
using FluentValidation;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Application;

public static class ResumeRules
{
    public const long MaxBytes = 10L * 1024 * 1024;
    public static readonly string[] Formats = { "PDF", "DOCX", "TXT" };

    public static bool IsSupportedFormat(string? format) => Formats.Contains(format?.Trim().ToUpperInvariant());
}

/// <summary>Parse -> standardise -> merge-into-reviewable-data pipeline for one resume (US-3.3.1-08..12). Runs inside the inbox transaction, so it never leaves half a result.</summary>
public sealed class ResumeParsingService(
    IResumeContentSource content, IResumeParser parser, ISkillTaxonomyProvider taxonomies, IResumeParsedDataRepository parsed,
    ISkillStandardizationRepository standardizations, IParsedProfileDataRepository profileData, IKnownProfileRepository knownProfiles,
    IMatchingConfigurationProvider configuration, TimeProvider clock, ILogger<ResumeParsingService> logger)
{
    public const string ModelVersionFallback = "rules-1";
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    public async Task ParseAsync(Guid resumeId, Guid profileId, string format, long sizeBytes, string sha256, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (await parsed.ExistsAsync(resumeId, sha256, ct))
        {
            return; // idempotent per (resumeId, sha256): a redelivered ResumeCreated changes nothing
        }

        var owner = await knownProfiles.GetAsync(profileId, ct)
                    ?? throw new InvalidOperationException($"Profile {profileId} is not known yet; retrying after ProfileCreated arrives.");
        var previous = await parsed.GetLatestByProfileAsync(profileId, ct);

        // INV-08: an unsupported/oversized/corrupt file is a Failed result (E-VBMA-UNSUPPORTED-FORMAT), published, not an endless retry.
        ParserResult? result = null;
        var stopwatch = Stopwatch.StartNew();
        if (ResumeRules.IsSupportedFormat(format) && sizeBytes <= ResumeRules.MaxBytes)
        {
            var file = await content.FetchAsync(resumeId, ct) ?? throw new InvalidOperationException($"Resume {resumeId} content is not available yet.");
            result = file.Bytes.LongLength > ResumeRules.MaxBytes ? null : await parser.ParseAsync(file.Bytes, format, ct);
        }

        var config = await configuration.GetAsync(ct);
        if (result is null)
        {
            var failed = ResumeParsedData.Unreadable(resumeId, profileId, sha256, ModelVersionFallback, now);
            parsed.Add(failed); // a failed parse never supersedes an earlier good one
            return;
        }

        var data = ResumeParsedData.FromParserResult(resumeId, profileId, sha256, result, config.LowConfidenceThresholdPercent, now);
        parsed.Add(data);
        if (previous is not null)
        {
            previous.Supersede(data.Id);
        }

        var run = SkillStandardization.Standardize(data.Id, profileId, result.Skills, await taxonomies.GetAsync(null, ct), now);
        standardizations.Add(run);

        var existing = await profileData.GetByProfileAsync(profileId, ct);
        if (existing is null)
        {
            profileData.Add(ParsedProfileData.Create(resumeId, profileId, owner.OwnerAccountId, data.Fields, now));
        }
        else
        {
            existing.ApplyReparse(resumeId, data.Fields); // INV-14: user corrections survive a re-parse
        }

        if (stopwatch.Elapsed > Budget)
        {
            logger.LogWarning("Parsing resume {ResumeId} took {ElapsedMs} ms (budget {BudgetMs} ms, THR-014)", resumeId, stopwatch.ElapsedMilliseconds, Budget.TotalMilliseconds);
        }
    }
}

// ---------------------------------------------------------------------- ParseResumeCommand (inbox: ResumeCreated)

public sealed record ParseResumeCommand(Guid ResumeId, Guid ProfileId, string Format, long SizeBytes, string Sha256) : ICommand;

public sealed class ParseResumeValidator : AbstractValidator<ParseResumeCommand>
{
    public ParseResumeValidator()
    {
        RuleFor(x => x.ResumeId).NotEmpty().WithErrorCode("VAL.ResumeId.Required");
        RuleFor(x => x.ProfileId).NotEmpty().WithErrorCode("VAL.ProfileId.Required");
        RuleFor(x => x.Sha256).NotEmpty().MaximumLength(64).WithErrorCode("VAL.Sha256.Required");
        RuleFor(x => x.SizeBytes).GreaterThanOrEqualTo(0).WithErrorCode("VAL.SizeBytes.Invalid");
        // Format and size limits (PDF/DOCX/TXT, 10 MB) are applied by the handler as an INV-08 Failed result, not as a request error.
    }
}

internal sealed class ParseResumeHandler(ResumeParsingService service) : ICommandHandler<ParseResumeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ParseResumeCommand request, CancellationToken ct)
    {
        await service.ParseAsync(request.ResumeId, request.ProfileId, request.Format, request.SizeBytes, request.Sha256, ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- StandardizeSkillsCommand (worker after PlatformTaxonomyUpdated / on demand)

/// <param name="ResumeParsedDataId">One parse to (re)standardise, or null to re-standardise a batch of runs that are not on the latest taxonomy version.</param>
public sealed record StandardizeSkillsCommand(Guid? ResumeParsedDataId, int BatchSize = 100) : ICommand<int>;

internal sealed class StandardizeSkillsHandler(ISkillStandardizationRepository standardizations, IResumeParsedDataRepository parsed, ISkillTaxonomyProvider taxonomies,
    TimeProvider clock) : ICommandHandler<StandardizeSkillsCommand, int>
{
    public async Task<Result<int>> Handle(StandardizeSkillsCommand request, CancellationToken ct)
    {
        var taxonomy = await taxonomies.GetAsync(null, ct); // captured once: the whole run keeps this version
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.ResumeParsedDataId is { } id)
        {
            var data = await parsed.GetAsync(id, ct);
            if (data is null)
            {
                return Error.NotFound(AiErrorCodes.NotFound, "The parsed data was not found.");
            }

            var existing = await standardizations.GetByResumeParsedDataAsync(id, ct);
            if (existing is null)
            {
                standardizations.Add(SkillStandardization.Standardize(id, data.ProfileId, data.Skills, taxonomy, now));
                return 1;
            }

            return existing.Restandardize(taxonomy, now) ? 1 : 0;
        }

        var stale = await standardizations.ListNotOnTaxonomyVersionAsync(taxonomy.Version, request.BatchSize, ct);
        return stale.Count(run => run.Restandardize(taxonomy, now));
    }
}

// ---------------------------------------------------------------------- correct / read parsed profile data (US-3.3.1-08/11/12)

public sealed record CorrectParsedProfileDataCommand(string Field, string Value) : SeekerRequest, ICommand;

public sealed record GetParsedProfileDataQuery : SeekerRequest, IQuery<ParsedProfileDataDto>;

public sealed class CorrectParsedProfileDataValidator : AbstractValidator<CorrectParsedProfileDataCommand>
{
    public static int MaxLength(ParsedFieldName name) => name switch
    {
        ParsedFieldName.PersonalDetails or ParsedFieldName.ContactInformation => 1000,
        ParsedFieldName.Skills => 2000,
        _ => 4000
    };

    public CorrectParsedProfileDataValidator()
    {
        RuleFor(x => x.Field).Must(f => Enum.TryParse<ParsedFieldName>(f, true, out _)).WithErrorCode("VAL.Field.NotAllowed");
        RuleFor(x => x.Value).NotEmpty().WithErrorCode("VAL.Value.Required");
        RuleFor(x => x).Must(x => !Enum.TryParse<ParsedFieldName>(x.Field, true, out var name) || (x.Value?.Length ?? 0) <= MaxLength(name))
            .OverridePropertyName("Value").WithErrorCode("VAL.Value.TooLong");
    }
}

internal sealed class CorrectParsedProfileDataHandler(IParsedProfileDataRepository repository, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CorrectParsedProfileDataCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CorrectParsedProfileDataCommand request, CancellationToken ct)
    {
        var data = user.UserId is { } owner ? await repository.GetByOwnerAsync(owner, ct) : null;
        if (data is null)
        {
            return Error.NotFound(AiErrorCodes.NotFound, "There is no parsed data to correct yet.");
        }

        data.Correct(Enum.Parse<ParsedFieldName>(request.Field, true), request.Value, user.ToActor(), clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

internal sealed class GetParsedProfileDataHandler(IMatchReadStore store, IMatchingConfigurationProvider configuration, ICurrentUser user)
    : IQueryHandler<GetParsedProfileDataQuery, ParsedProfileDataDto>
{
    public async Task<Result<ParsedProfileDataDto>> Handle(GetParsedProfileDataQuery request, CancellationToken ct)
    {
        var dto = user.UserId is { } owner ? await store.GetParsedProfileDataAsync(owner, ct) : null;
        if (dto is null)
        {
            return Error.NotFound(AiErrorCodes.NotFound, "There is no parsed data yet.");
        }

        return dto with { LowConfidenceThresholdPercent = (await configuration.GetAsync(ct)).LowConfidenceThresholdPercent };
    }
}
