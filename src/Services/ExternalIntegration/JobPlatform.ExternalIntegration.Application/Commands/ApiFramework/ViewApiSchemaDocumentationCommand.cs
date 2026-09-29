using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

/// <summary>Modelled as a command (not a plain query) because viewing the schema is a side effect: it logs the view and publishes
/// ApiSchemaDocumentationViewed (handover Q-03) — a deliberate deviation from "GET routes are queries" to reuse the pipeline's
/// unit-of-work commit; see the BC-02 status doc.</summary>
public sealed record ViewApiSchemaDocumentationCommand(string Version) : PartnerCommand<ApiSchemaDocumentationView>;
