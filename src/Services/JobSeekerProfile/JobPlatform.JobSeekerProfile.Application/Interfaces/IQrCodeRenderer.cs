namespace JobPlatform.JobSeekerProfile.Application.Interfaces;

/// <summary>Renders a share link as a scannable image. See docs/bc-status/BC-04.md for the current limitation.</summary>
public interface IQrCodeRenderer
{
    string RenderSvg(string url);
}
