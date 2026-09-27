using JobPlatform.TestSupport;

namespace JobPlatform.Notification.Api.IntegrationTests;

/// <summary>Hosts the real Notification API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS, fake provider/contact adapters).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "Notification";

    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["Contacts:Provider"] = "Fake";
        settings["Notification:DispatcherEnabled"] = "false";
        settings["Notification:DigestEnabled"] = "false";
        return settings;
    }
}
