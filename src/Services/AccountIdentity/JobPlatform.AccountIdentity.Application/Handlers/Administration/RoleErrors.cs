using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

/// <summary>Logic shared by the role request handlers.</summary>
internal static class RoleErrors
{
    public static readonly Error RoleNotFound = Error.NotFound("E-ROLE-NOT-FOUND", "The role was not found.");
}
