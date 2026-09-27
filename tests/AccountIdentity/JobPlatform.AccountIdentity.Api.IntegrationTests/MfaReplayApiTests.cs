using System.Net;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>A TOTP code is single use: replaying an accepted code (same 30 s step) is refused with the generic sign-in error.</summary>
public class MfaReplayApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MfaReplayApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_Mfa_ReplayingAnAlreadyAcceptedCode_IsRefused_ButTheNextCodeWorks()
    {
        var client = new ApiClient(_factory);
        await client.AdminAsync();
        var secret = _factory.AdminMfaSecret!;
        var replayed = _factory.Totp.Current(secret);

        var replay = await client.PostAsync("/api/v1/auth/login",
            new { username = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword, mechanism = "mfa", mfaCode = replayed });
        var fresh = await client.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword, "mfa", _factory.Totp.For(secret));

        await replay.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");
        fresh["status"]!.GetValue<string>().Should().Be("Authenticated");
    }
}
