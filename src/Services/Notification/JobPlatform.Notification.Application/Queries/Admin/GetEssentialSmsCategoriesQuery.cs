using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Queries.Admin;

public sealed record GetEssentialSmsCategoriesQuery : AdminRequest, IQuery<IReadOnlyCollection<string>>
{
    public GetEssentialSmsCategoriesQuery() : base(NotificationErrorCodes.SmsForbidden)
    {
    }
}
