using FluentValidation.TestHelper;
using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Application.Commands.Registration;
using JobPlatform.EmployerOnboarding.Application.Validators.Media;
using JobPlatform.EmployerOnboarding.Application.Validators.Registration;
using JobPlatform.EmployerOnboarding.Domain;

namespace JobPlatform.EmployerOnboarding.Application.UnitTests;

public class ValidatorTests
{
    private readonly SubmitEmployerLevel2Validator _level2 = new();
    private readonly AttachCompanyMediaValidator _media = new();
    private readonly ApproveEmployerRegistrationValidator _approve = new();

    private static SubmitEmployerLevel2Command ValidCommand() =>
        new("Acme Ltd", "CO-123", "REG-456", "https://acme.example", "Software", CompanySize.Small, "Ramallah", "Ramallah", null, "A company.");

    [Fact]
    public void SubmitEmployerLevel2_Valid_HasNoErrors() => _level2.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void SubmitEmployerLevel2_MissingCompanyName_HasError() =>
        _level2.TestValidate(ValidCommand() with { CompanyName = "" }).ShouldHaveValidationErrorFor(c => c.CompanyName);

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://acme.example")]
    [InlineData("")]
    public void SubmitEmployerLevel2_InvalidWebsite_HasError(string website) =>
        _level2.TestValidate(ValidCommand() with { Website = website }).ShouldHaveValidationErrorFor(c => c.Website);

    [Fact]
    public void SubmitEmployerLevel2_DescriptionTooLong_HasError() =>
        _level2.TestValidate(ValidCommand() with { Description = new string('a', 2001) }).ShouldHaveValidationErrorFor(c => c.Description);

    [Fact]
    public void AttachCompanyMedia_Valid_HasNoErrors() =>
        _media.TestValidate(new AttachCompanyMediaCommand(MediaKind.Logo, "logo.png", "image/png", 1024, new byte[1024])).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AttachCompanyMedia_TooLarge_HasError() =>
        _media.TestValidate(new AttachCompanyMediaCommand(MediaKind.Logo, "logo.png", "image/png", CompanyMediaAndDocument.MaxSizeBytes + 1, Array.Empty<byte>()))
            .ShouldHaveValidationErrorFor(c => c.SizeBytes);

    [Fact]
    public void AttachCompanyMedia_EmptyFileName_HasError() =>
        _media.TestValidate(new AttachCompanyMediaCommand(MediaKind.Logo, "", "image/png", 1024, new byte[1024])).ShouldHaveValidationErrorFor(c => c.FileName);

    [Fact]
    public void ApproveEmployerRegistration_EmptyId_HasError() =>
        _approve.TestValidate(new ApproveEmployerRegistrationCommand(Guid.Empty)).ShouldHaveValidationErrorFor(c => c.EmployerRegistrationId);

    [Fact]
    public void ApproveEmployerRegistration_ValidId_HasNoErrors() =>
        _approve.TestValidate(new ApproveEmployerRegistrationCommand(Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
}
