using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Adapters;

/// <summary>
/// Default adapters for the anti-corruption ports (handover section 4.3). The exact external MoL/PEF/government-database/institution/ID-system
/// APIs are unknown (handover Q-04 - "adapters are stubbed against WireMock until specs arrive"); this environment has no WireMock harness
/// available either, so these are deterministic stand-ins that always report a match. They exist so the Api boots and its wiring is exercised
/// end-to-end; Application-layer tests substitute fakes (NSubstitute) to exercise the Match/NoMatch/Unavailable branches, and
/// Api.IntegrationTests substitute test doubles via <c>ConfigureTestServices</c> to exercise the same branches over HTTP.
/// </summary>
internal sealed class StubMolRegistryClient : IMolRegistryClient
{
    public Task<EmployerVerificationCheckResult> VerifyEmployerAsync(Submission submission, CancellationToken ct) =>
        Task.FromResult(new EmployerVerificationCheckResult(SourceCallOutcome.Match, null));

    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(SourceCallOutcome.Match, new Dictionary<string, string> { ["source"] = "MoL" }, null));

    public Task<SourceSyncResult> SyncAsync(CancellationToken ct) => Task.FromResult(new SourceSyncResult(true, $"mol-snapshot-{Guid.NewGuid():N}", null));
}

internal sealed class StubPefClient : IPefClient
{
    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(SourceCallOutcome.Match, new Dictionary<string, string> { ["source"] = "PEF" }, null));

    public Task<SourceSyncResult> SyncAsync(CancellationToken ct) => Task.FromResult(new SourceSyncResult(true, $"pef-snapshot-{Guid.NewGuid():N}", null));
}

internal sealed class StubGovernmentDatabaseClient : IGovernmentDatabaseClient
{
    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(SourceCallOutcome.Match, new Dictionary<string, string> { ["source"] = "GovernmentDatabase" }, null));
}

internal sealed class StubEducationalInstitutionClient : IEducationalInstitutionClient
{
    public Task<EducationalCheckResult> CheckAsync(Credential credential, CancellationToken ct) =>
        Task.FromResult(new EducationalCheckResult(SourceCallOutcome.Match, null));
}

internal sealed class StubGovernmentIdClient : IGovernmentIdClient
{
    public Task<IdentityCheckResult> CheckAsync(IdentityClaim claim, CancellationToken ct) =>
        Task.FromResult(new IdentityCheckResult(SourceCallOutcome.Match, false, null));
}

/// <summary>No legacy source is reachable in this environment; the default adapter reports an empty page so
/// <see cref="ImportLegacyDataBatchCommand"/> is a safe no-op until a real MoL/PEF extract is wired in.</summary>
internal sealed class StubLegacySourceReader : ILegacySourceReader
{
    public Task<IReadOnlyList<LegacySourceRecord>> ReadBatchAsync(SourceSystem sourceSystem, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LegacySourceRecord>>(Array.Empty<LegacySourceRecord>());
}
