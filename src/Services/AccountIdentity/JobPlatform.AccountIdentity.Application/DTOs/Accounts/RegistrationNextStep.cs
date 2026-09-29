namespace JobPlatform.AccountIdentity.Application.DTOs.Accounts;

/// <summary>Values of <see cref="RegisteredAccountDto.NextStep"/>.</summary>
public static class RegistrationNextStep
{
    public const string ActivateWithMobileCode = "ActivateWithMobileCode";
    public const string AwaitStaffApproval = "AwaitStaffApproval";
}
