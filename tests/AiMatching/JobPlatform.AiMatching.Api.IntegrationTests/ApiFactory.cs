using JobPlatform.TestSupport;

namespace JobPlatform.AiMatching.Api.IntegrationTests;

/// <summary>Hosts the real AI Matching API in-process (SQLite, in-memory cache and bus, fake clock).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "AiMatching";
}
