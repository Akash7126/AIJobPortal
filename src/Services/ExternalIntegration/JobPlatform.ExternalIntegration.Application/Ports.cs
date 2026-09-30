namespace JobPlatform.ExternalIntegration.Application;

/// <summary>One partner job as returned by the pull feed, in the partner's own (non-standard) shape: raw field name/value pairs a
/// JobDataMapping translates. Partner shapes never leave this adapter boundary (handover 4.3, anti-corruption layer).</summary>
public sealed record PartnerJobPayload(string SourceJobId, IReadOnlyDictionary<string, string> Fields);
