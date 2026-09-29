using JobPlatform.Notification.Application.Commands.Admin;
using JobPlatform.Notification.Application.Services.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class ConfigureEssentialSmsCategoriesHandler : ICommandHandler<ConfigureEssentialSmsCategoriesCommand, IReadOnlyCollection<string>>
{
    private readonly ISmsPolicyRepository _policy;
    private readonly AdminService _adminService;

    public ConfigureEssentialSmsCategoriesHandler(ISmsPolicyRepository policy, AdminService adminService)
    {
        _policy = policy;
        _adminService = adminService;
    }

    public async Task<Result<IReadOnlyCollection<string>>> Handle(ConfigureEssentialSmsCategoriesCommand request, CancellationToken ct)
    {
        var next = (await _policy.GetCurrentAsync(ct)).NewVersion(_adminService.Me, request.Categories, _adminService.Now);
        _policy.Add(next);
        return Result.Success(next.EssentialCategories);
    }
}
