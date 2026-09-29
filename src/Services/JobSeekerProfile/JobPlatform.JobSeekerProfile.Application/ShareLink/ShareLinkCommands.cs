namespace JobPlatform.JobSeekerProfile.Application.ShareLink;

/// <summary>Public site base URL used to build a share link (e.g. https://jobs.example) - Api:PublicBaseUrl in configuration.</summary>
public sealed class PublicSiteOptions
{
    public const string SectionName = "Api";

    public string PublicBaseUrl { get; set; } = "https://jobplatform.local";
}
