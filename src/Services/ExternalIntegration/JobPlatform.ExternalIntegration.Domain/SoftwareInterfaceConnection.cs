using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>Proposed entity/aggregate (US-4.3-01): the catalogue of outward interfaces by category (external job sites, government DBs,
/// email/SMS gateways, analytics/reporting tools). Registration is idempotent (no duplicate connection record, AC-05) and configured only
/// by authorised platform operators (AC-04).</summary>
public sealed class SoftwareInterfaceConnection : AggregateRoot<Guid>
{
    private SoftwareInterfaceConnection()
    {
    }

    public SoftwareInterfaceCategory Category { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }

    public static SoftwareInterfaceConnection Register(Guid id, SoftwareInterfaceCategory category, string name, string endpoint, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.SoftwareInterfaceAdminOnly, ErrorCodes.SoftwareInterfaceForbidden));
        return new SoftwareInterfaceConnection { Id = id, Category = category, Name = name, Endpoint = endpoint, Enabled = true };
    }

    public void UpdateEndpoint(string endpoint, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.SoftwareInterfaceAdminOnly, ErrorCodes.SoftwareInterfaceForbidden));
        Endpoint = endpoint;
    }

    public void SetEnabled(bool enabled, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.SoftwareInterfaceAdminOnly, ErrorCodes.SoftwareInterfaceForbidden));
        Enabled = enabled;
    }
}
