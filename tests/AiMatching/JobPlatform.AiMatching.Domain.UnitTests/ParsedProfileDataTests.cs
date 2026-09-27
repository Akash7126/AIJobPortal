using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class ParsedProfileDataTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static ParsedProfileData Create(Guid ownerAccountId, Guid profileId) =>
        ParsedProfileData.Create(Guid.NewGuid(), profileId, ownerAccountId,
            new[] { new ParsedField(ParsedFieldName.Skills, "C#, SQL", 90, false) }, At);

    [Fact]
    public void Create_FieldsAreSourcedFromParser()
    {
        var data = Create(Guid.NewGuid(), Guid.NewGuid());

        data.Status.Should().Be(ReviewStatus.Parsed);
        data.Fields.Single().Source.Should().Be(FieldSource.Parser);
    }

    [Fact]
    public void Correct_ByOwner_ReplacesValueAndMarksReviewed()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        var actor = new Actor(owner, ActorType.JobSeeker);

        data.Correct(ParsedFieldName.Skills, "C#, SQL, Azure", actor, At.AddMinutes(1));

        var field = data.Fields.Single(f => f.Name == ParsedFieldName.Skills);
        field.Value.Should().Be("C#, SQL, Azure");
        field.Source.Should().Be(FieldSource.User);
        field.Confidence.Should().Be(100);
        field.NeedsReview.Should().BeFalse();
        data.Status.Should().Be(ReviewStatus.Reviewed);
    }

    [Fact]
    public void Correct_NewField_IsAddedAsUserSourced()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        var actor = new Actor(owner, ActorType.JobSeeker);

        data.Correct(ParsedFieldName.Certifications, "PMP", actor, At);

        data.Fields.Should().ContainSingle(f => f.Name == ParsedFieldName.Certifications && f.Source == FieldSource.User);
    }

    [Fact]
    public void Correct_ByNonOwner_ThrowsForbidden()
    {
        var data = Create(Guid.NewGuid(), Guid.NewGuid());
        var stranger = new Actor(Guid.NewGuid(), ActorType.JobSeeker);

        var act = () => data.Correct(ParsedFieldName.Skills, "C#", stranger, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AiRuleCodes.ParsedNotOwner);
        ex.ExternalCode.Should().Be(AiErrorCodes.Forbidden);
    }

    [Fact]
    public void Correct_NonJobSeekerActor_ThrowsForbiddenEvenWithMatchingId()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        var wrongType = new Actor(owner, ActorType.Employer);

        var act = () => data.Correct(ParsedFieldName.Skills, "C#", wrongType, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.ParsedNotOwner);
    }

    [Fact]
    public void Correct_EmptyValue_ThrowsInvalidField()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        var actor = new Actor(owner, ActorType.JobSeeker);

        var act = () => data.Correct(ParsedFieldName.Skills, "   ", actor, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.ParsedInvalidField);
    }

    [Fact]
    public void Correct_SkillsField_RaisesEventWithParsedSkillsList()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        var actor = new Actor(owner, ActorType.JobSeeker);

        data.Correct(ParsedFieldName.Skills, "C#, Azure", actor, At);

        var evt = data.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ParsedProfileDataUpdatedDomainEvent>().Which;
        evt.ChangedFields.Should().Equal(nameof(ParsedFieldName.Skills));
        evt.Skills.Should().Equal("C#", "Azure");
        evt.ActorId.Should().Be(owner);
    }

    [Fact]
    public void Correct_NonSkillsField_RaisesEventWithEmptySkills()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        var actor = new Actor(owner, ActorType.JobSeeker);

        data.Correct(ParsedFieldName.Achievements, "Employee of the year", actor, At);

        data.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ParsedProfileDataUpdatedDomainEvent>()
            .Which.Skills.Should().BeEmpty();
    }

    [Fact]
    public void ApplyReparse_UserSourcedField_IsNeverOverwritten()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());
        data.Correct(ParsedFieldName.Skills, "C#, Azure (corrected)", new Actor(owner, ActorType.JobSeeker), At);

        var changed = data.ApplyReparse(Guid.NewGuid(), new[] { new ParsedField(ParsedFieldName.Skills, "Reparsed value", 50, true) });

        changed.Should().BeEmpty();
        data.Fields.Single(f => f.Name == ParsedFieldName.Skills).Value.Should().Be("C#, Azure (corrected)");
    }

    [Fact]
    public void ApplyReparse_ParserSourcedField_IsUpdated()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());

        var changed = data.ApplyReparse(Guid.NewGuid(), new[] { new ParsedField(ParsedFieldName.Skills, "C#, SQL, Docker", 95, false) });

        changed.Should().Equal(ParsedFieldName.Skills);
        data.Fields.Single(f => f.Name == ParsedFieldName.Skills).Value.Should().Be("C#, SQL, Docker");
    }

    [Fact]
    public void ApplyReparse_NewField_IsAddedAsParserSourced()
    {
        var owner = Guid.NewGuid();
        var data = Create(owner, Guid.NewGuid());

        var changed = data.ApplyReparse(Guid.NewGuid(), new[] { new ParsedField(ParsedFieldName.WorkExperience, "5 years", 80, false) });

        changed.Should().Equal(ParsedFieldName.WorkExperience);
        data.Fields.Should().Contain(f => f.Name == ParsedFieldName.WorkExperience && f.Source == FieldSource.Parser);
    }

    [Fact]
    public void ApplyReparse_UpdatesResumeId()
    {
        var data = Create(Guid.NewGuid(), Guid.NewGuid());
        var newResumeId = Guid.NewGuid();

        data.ApplyReparse(newResumeId, Array.Empty<ParsedField>());

        data.ResumeId.Should().Be(newResumeId);
    }
}
