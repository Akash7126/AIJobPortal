using JobPlatform.TestSupport;

namespace JobPlatform.Reporting.Api.IntegrationTests;

/// <summary>Hosts the real Reporting API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS, simulated metrics/sessions/PowerBI adapters).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "Reporting";

    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["Metrics:Provider"] = "Simulated";
        settings["Sessions:Provider"] = "Simulated";
        settings["PowerBi:Provider"] = "Simulated";
        return settings;
    }
}
