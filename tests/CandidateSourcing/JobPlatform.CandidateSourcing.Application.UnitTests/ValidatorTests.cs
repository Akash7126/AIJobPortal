using FluentValidation.TestHelper;
using JobPlatform.CandidateSourcing.Application.Search;
using JobPlatform.CandidateSourcing.Application.TalentPool;
using JobPlatform.CandidateSourcing.Application.Threshold;

namespace JobPlatform.CandidateSourcing.Application.UnitTests;

public class ValidatorTests
{
    [Fact]
    public void AddToTalentPool_MissingIds_FailValidation()
    {
        var result = new AddToTalentPoolValidator().TestValidate(new AddToTalentPoolCommand(Guid.Empty, Guid.Empty, null));

        result.ShouldHaveValidationErrorFor(c => c.CandidateProfileId);
        result.ShouldHaveValidationErrorFor(c => c.JobPostingId);
    }

    [Fact]
    public void AddToTalentPool_NoteTooLong_FailsValidation()
    {
        var result = new AddToTalentPoolValidator().TestValidate(new AddToTalentPoolCommand(Guid.NewGuid(), Guid.NewGuid(), new string('x', 501)));

        result.ShouldHaveValidationErrorFor(c => c.Note);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetQualificationThreshold_OutOfRange_FailsValidation(int percent)
    {
        var result = new SetQualificationThresholdValidator().TestValidate(new SetQualificationThresholdCommand(Guid.NewGuid(), percent));

        result.ShouldHaveValidationErrorFor(c => c.Percent);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    [Trait("AC", "AC-02")]
    public void SearchCandidateDatabase_InvertedSalaryRange_FailsValidation()
    {
        var criteria = new CandidateSearchCriteria(null, null, null, null, null, 5000m, 1000m, null);

        var result = new SearchCandidateDatabaseValidator().TestValidate(new SearchCandidateDatabaseQuery(criteria, 1, 20));

        result.ShouldHaveValidationErrorFor(q => q.Criteria);
    }

    [Fact]
    public void SearchCandidateDatabase_PageSizeTooLarge_FailsValidation()
    {
        var criteria = new CandidateSearchCriteria(null, null, null, null, null, null, null, null);

        var result = new SearchCandidateDatabaseValidator().TestValidate(new SearchCandidateDatabaseQuery(criteria, 1, 200));

        result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void SearchCandidateDatabase_ValidCriteria_Passes()
    {
        var criteria = new CandidateSearchCriteria(new[] { "sql" }, "Bachelor", 1m, 5m, "PS-RAM", 1000m, 3000m, "Immediate");

        var result = new SearchCandidateDatabaseValidator().TestValidate(new SearchCandidateDatabaseQuery(criteria, 1, 20));

        result.ShouldNotHaveValidationErrorFor(q => q.Criteria);
    }
}
