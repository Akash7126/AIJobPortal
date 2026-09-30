using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

/// <summary>Request that requires an authenticated caller and (optionally) roles/permission. Requests without it are anonymous.</summary>
public interface IAuthorizedRequest
{
    /// <summary>Empty = any authenticated actor type.</summary>
    IReadOnlyCollection<ActorType> AllowedActorTypes => Array.Empty<ActorType>();

    string? RequiredPermission => null;

    bool RequireMfa => false;

    /// <summary>External error code returned when access is refused.</summary>
    string ForbiddenErrorCode => "E-AAFR-FORBIDDEN";
}
