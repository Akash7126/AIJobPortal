using System.Diagnostics;
using JobPlatform.AiMatching.Domain;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Application.Services.Parsing;

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
