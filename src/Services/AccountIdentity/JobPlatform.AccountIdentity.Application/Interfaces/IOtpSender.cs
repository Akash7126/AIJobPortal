using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Delivers the mobile activation code. Q-03: BC-13 does not yet consume an OTP request, so this port has a dev/log adapter.</summary>
public interface IOtpSender
{
    Task SendActivationCodeAsync(MobileNumber mobile, string code, Language language, CancellationToken ct = default);
}
