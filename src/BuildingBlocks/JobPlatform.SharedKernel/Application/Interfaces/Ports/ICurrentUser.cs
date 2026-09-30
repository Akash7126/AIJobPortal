using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.SharedKernel.Application.Interfaces.Ports;

/// <summary>The authenticated caller, as seen by the application layer. Implemented by the Api from the validated token.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    ActorType? ActorType { get; }
    IReadOnlyCollection<Guid> RoleIds { get; }
    Guid? SessionId { get; }
    bool MfaVerified { get; }
    bool MustChangePassword { get; }
    string? ClientId { get; }
    IReadOnlyCollection<string> Scopes { get; }
    string? TokenId { get; }

    /// <summary>Caller network source (IP + optional device id) used for rate limiting.</summary>
    string SourceKey { get; }
    string? IpAddress { get; }
    Language Language { get; }
}
