using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Queries.Administration;

public sealed record GetSessionTimeoutQuery : AdminAuthorized, IQuery<SessionTimeoutView>
{
    public GetSessionTimeoutQuery() : base(Permissions.SessionTimeoutManage)
    {
    }
}
