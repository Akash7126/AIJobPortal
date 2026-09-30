namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Signs and verifies expiring report links (the distribution event carries a link, never the content).</summary>
public interface IReportLinkSigner
{
    string Sign(Guid exportId, DateTime expiresUtc);

    bool Verify(Guid exportId, long expiresUnix, string signature, DateTime nowUtc);
}
