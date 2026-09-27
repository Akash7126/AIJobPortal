using FluentValidation.TestHelper;
using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Application.Entities;
using JobPlatform.PlatformAdministration.Application.Offerings;
using JobPlatform.PlatformAdministration.Application.Reference;
using JobPlatform.PlatformAdministration.Application.Settings;
using JobPlatform.PlatformAdministration.Application.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Users;
using JobPlatform.PlatformAdministration.Domain.Entities;

namespace JobPlatform.PlatformAdministration.Application.UnitTests;

public class ValidatorTests
{
    private static LocalizedNameView Name => new("عربي", "English");

    [Fact]
    public void EntityRecord_ValidJobSeeker_Passes()
    {
        var command = new CreatePlatformEntityRecordCommand(PlatformEntityType.JobSeeker, new() { ["fullName"] = "Layla", ["mobile"] = "+970590000001" });

        new CreatePlatformEntityRecordValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EntityRecord_MissingRequiredFields_ReportsFieldCodes()
    {
        var command = new CreatePlatformEntityRecordCommand(PlatformEntityType.Employer, new() { ["companyName"] = "Acme" });

        var result = new CreatePlatformEntityRecordValidator().TestValidate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Core.companyId" && e.ErrorCode == "VAL.CompanyId.Required");
    }

    [Theory]
    [InlineData("mobile", "12345", "VAL.MobileNumber.Invalid")]
    [InlineData("email", "not-an-email", "VAL.Email.Invalid")]
    public void EntityRecord_InvalidFormats_AreReported(string field, string value, string code)
    {
        var command = new CreatePlatformEntityRecordCommand(PlatformEntityType.JobSeeker, new() { ["fullName"] = "L", ["mobile"] = "+970590000001", [field] = value });

        new CreatePlatformEntityRecordValidator().TestValidate(command).Errors.Should().Contain(e => e.ErrorCode == code);
    }

    [Fact]
    public void EntityRecord_NullCoreOrTooManyFields_Fails()
    {
        var validator = new CreatePlatformEntityRecordValidator();
        validator.TestValidate(new CreatePlatformEntityRecordCommand(PlatformEntityType.JobSeeker, null)).ShouldHaveValidationErrorFor(c => c.Core);
        var many = Enumerable.Range(0, 21).ToDictionary(i => $"f{i}", _ => "v");
        validator.TestValidate(new CreatePlatformEntityRecordCommand(PlatformEntityType.JobSeeker, many)).Errors.Should().Contain(e => e.ErrorCode == "VAL.Core.TooManyFields");
    }

    [Fact]
    public void EntityRecord_UndefinedEntityType_Fails()
    {
        new CreatePlatformEntityRecordValidator().TestValidate(new CreatePlatformEntityRecordCommand((PlatformEntityType)99, new()))
            .ShouldHaveValidationErrorFor(c => c.EntityType);
    }

    [Theory]
    [InlineData("nope", "1", "VAL.Key.Unknown")]
    [InlineData("upload.maxSizeMb", "abc", "VAL.Value.InvalidType")]
    [InlineData("upload.maxSizeMb", null, "VAL.Value.Required")]
    [InlineData("platform.maintenanceMode", "perhaps", "VAL.Value.InvalidType")]
    public void Setting_Invalid_ReportsCode(string key, string? value, string code)
    {
        new ChangeSystemSettingValidator().TestValidate(new ChangeSystemSettingCommand(key, value)).Errors.Should().Contain(e => e.ErrorCode == code);
    }

    [Fact]
    public void Setting_OutOfBoundsButParsable_PassesValidation()
    {
        // Bounds are a domain rule (INV-08), not a validator rule.
        new ChangeSystemSettingValidator().TestValidate(new ChangeSystemSettingCommand("upload.maxSizeMb", "9999")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ReferenceFile_ValidChanges_Pass()
    {
        var command = new UpdateReferenceFileCommand("skills", false, new[]
        {
            new ReferenceChangeRequest("add", null, "python", Name, null),
            new ReferenceChangeRequest("edit", Guid.NewGuid(), null, Name, null),
            new ReferenceChangeRequest("Remove", Guid.NewGuid(), null, null, null)
        });

        new UpdateReferenceFileValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("bogus", "VAL.Type.Invalid")]
    public void ReferenceFile_UnknownType_Fails(string type, string code)
    {
        var command = new UpdateReferenceFileCommand(type, false, new[] { new ReferenceChangeRequest("add", null, "a", Name, null) });

        new UpdateReferenceFileValidator().TestValidate(command).Errors.Should().Contain(e => e.ErrorCode == code);
    }

    [Fact]
    public void ReferenceFile_DuplicateCodesNamesAndMissingIdsInRequest_Fail()
    {
        var command = new UpdateReferenceFileCommand("skills", false, new[]
        {
            new ReferenceChangeRequest("add", null, "a", Name, null),
            new ReferenceChangeRequest("add", null, "A", new LocalizedNameView("", new string('x', 201)), null),
            new ReferenceChangeRequest("remove", null, null, null, null),
            new ReferenceChangeRequest("edit", Guid.NewGuid(), null, null, null),
            new ReferenceChangeRequest("explode", null, null, null, null)
        });

        var codes = new UpdateReferenceFileValidator().TestValidate(command).Errors.Select(e => e.ErrorCode).ToList();

        codes.Should().Contain(new[] { "VAL.Code.Duplicate", "VAL.Name.Ar.Invalid", "VAL.Name.En.Invalid", "VAL.EntryId.Required", "VAL.Change.Empty", "VAL.Op.Invalid" });
    }

    [Fact]
    public void ReferenceFile_NoChanges_Fails()
    {
        new UpdateReferenceFileValidator().TestValidate(new UpdateReferenceFileCommand("skills", false, Array.Empty<ReferenceChangeRequest>()))
            .Errors.Should().Contain(e => e.ErrorCode == "VAL.Changes.OutOfRange");
    }

    [Fact]
    public void Taxonomy_ValidChanges_Pass()
    {
        var command = new UpdatePlatformTaxonomyCommand("skills", new[]
        {
            new TaxonomyChangeRequest("add", "python", Name, null, new[] { "py" }, null),
            new TaxonomyChangeRequest("add", "django", Name, "python", null, null),
            new TaxonomyChangeRequest("remove", "java", null, null, null, null)
        });

        new UpdatePlatformTaxonomyValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Taxonomy_InvalidRequests_ReportCodes()
    {
        var command = new UpdatePlatformTaxonomyCommand("Bad Type", new[]
        {
            new TaxonomyChangeRequest("add", "a", null, "a", new[] { "" }, null),
            new TaxonomyChangeRequest("add", "A", Name, null, null, null),
            new TaxonomyChangeRequest("add", "bad code!", Name, null, null, null),
            new TaxonomyChangeRequest("weird", "", null, null, null, null)
        });

        var codes = new UpdatePlatformTaxonomyValidator().TestValidate(command).Errors.Select(e => e.ErrorCode).ToList();

        codes.Should().Contain(new[] { "VAL.Type.Invalid", "VAL.Name.Required", "VAL.ParentCode.Self", "VAL.Synonyms.Invalid", "VAL.Code.Duplicate", "VAL.Code.InvalidFormat", "VAL.Op.Invalid", "VAL.Code.Required" });
    }

    [Theory]
    [InlineData("", "VAL.Reason.Required")]
    [InlineData("abc", "VAL.Reason.OutOfRange")]
    public void Suspend_InvalidReason_Fails(string reason, string code)
    {
        new SuspendJobOfferingValidator().TestValidate(new SuspendJobOfferingCommand(Guid.NewGuid(), reason)).Errors.Should().Contain(e => e.ErrorCode == code);
        new RemoveJobOfferingValidator().TestValidate(new RemoveJobOfferingCommand(Guid.NewGuid(), reason)).Errors.Should().Contain(e => e.ErrorCode == code);
    }

    [Fact]
    public void Suspend_ValidAndEmptyId()
    {
        new SuspendJobOfferingValidator().TestValidate(new SuspendJobOfferingCommand(Guid.NewGuid(), "Valid reason")).ShouldNotHaveAnyValidationErrors();
        new SuspendJobOfferingValidator().TestValidate(new SuspendJobOfferingCommand(Guid.Empty, "Valid reason")).ShouldHaveValidationErrorFor(c => c.JobOfferingId);
        new SuspendJobOfferingValidator().TestValidate(new SuspendJobOfferingCommand(Guid.NewGuid(), new string('x', 501))).ShouldHaveValidationErrorFor(c => c.Reason);
    }

    [Theory]
    [InlineData(0, 20, "VAL.Page.OutOfRange")]
    [InlineData(1, 101, "VAL.PageSize.OutOfRange")]
    public void ListOfferings_Paging_Fails(int page, int size, string code)
    {
        new ListJobOfferingsValidator().TestValidate(new ListJobOfferingsQuery(null, page, size)).Errors.Should().Contain(e => e.ErrorCode == code);
    }

    [Fact]
    public void ListOfferings_StatusFilter()
    {
        var validator = new ListJobOfferingsValidator();
        validator.TestValidate(new ListJobOfferingsQuery("inactive")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ListJobOfferingsQuery("gone")).ShouldHaveValidationErrorFor(q => q.Status);
    }

    [Fact]
    public void ListUsers_Filters()
    {
        var validator = new ListPlatformUsersValidator();
        validator.TestValidate(new ListPlatformUsersQuery("l", "employer", "Active")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ListPlatformUsersQuery(null, "alien", null)).ShouldHaveValidationErrorFor(q => q.Type);
        validator.TestValidate(new ListPlatformUsersQuery(new string('x', 101), null, null)).ShouldHaveValidationErrorFor(q => q.Search);
        validator.TestValidate(new ListPlatformUsersQuery(null, null, null, 1, 500)).ShouldHaveValidationErrorFor(q => q.PageSize);
    }
}
