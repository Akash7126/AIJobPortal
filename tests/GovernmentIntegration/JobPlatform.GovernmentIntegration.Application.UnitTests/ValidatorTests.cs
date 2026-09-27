using FluentValidation.TestHelper;
using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

public class ValidatorTests
{
    [Fact]
    public void RequestEmployerVerificationValidator_MissingFields_FailsWithExpectedCodes()
    {
        var validator = new RequestEmployerVerificationValidator();

        var result = validator.TestValidate(new RequestEmployerVerificationCommand("", "", "", null));

        result.ShouldHaveValidationErrorFor(c => c.RegistrationNumber);
        result.ShouldHaveValidationErrorFor(c => c.VatNumber);
        result.ShouldHaveValidationErrorFor(c => c.MobileNumber);
    }

    [Fact]
    public void RequestEmployerVerificationValidator_ValidCommand_Passes()
    {
        var validator = new RequestEmployerVerificationValidator();

        var result = validator.TestValidate(new RequestEmployerVerificationCommand("REG-1", "VAT-1", "+970591234567", null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DecideEmployerVerificationManuallyValidator_RejectWithoutReason_Fails()
    {
        var validator = new DecideEmployerVerificationManuallyValidator();

        var result = validator.TestValidate(new DecideEmployerVerificationManuallyCommand(Guid.NewGuid(), ManualDecision.Reject, null));

        result.ShouldHaveValidationErrorFor(c => c.Reason);
    }

    [Fact]
    public void ConfigureGovernmentSourceConnectionValidator_NonHttpsEndpoint_Fails()
    {
        var validator = new ConfigureGovernmentSourceConnectionValidator();

        var result = validator.TestValidate(new ConfigureGovernmentSourceConnectionCommand(SourceSystem.MoL, "http://insecure.example", "ApiKey", "ref", true));

        result.ShouldHaveValidationErrorFor(c => c.Endpoint);
    }

    [Fact]
    public void RequestEducationalCredentialVerificationValidator_YearInFuture_Fails()
    {
        var validator = new RequestEducationalCredentialVerificationValidator();

        var result = validator.TestValidate(new RequestEducationalCredentialVerificationCommand("component", Guid.NewGuid(), "Institution", "Degree",
            DateTime.UtcNow.Year + 1));

        result.ShouldHaveValidationErrorFor(c => c.Year);
    }

    [Fact]
    public void StartDataMigrationValidator_EmptyPhases_Fails()
    {
        var validator = new StartDataMigrationValidator();

        var result = validator.TestValidate(new StartDataMigrationCommand(Array.Empty<string>(), false));

        result.ShouldHaveValidationErrorFor(c => c.Phases);
    }
}
