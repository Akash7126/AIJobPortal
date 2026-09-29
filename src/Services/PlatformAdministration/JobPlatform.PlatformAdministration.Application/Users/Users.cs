namespace JobPlatform.PlatformAdministration.Application.Users;

public static class UserFilters
{
    public static readonly IReadOnlyList<string> Types = new[] { "JobSeeker", "Employer", "Administrator" };
    public const int MaxSearchLength = 100;
}
