namespace JobPlatform.PlatformAdministration.Application.Commands.Offerings;

/// <summary>US-3.1.4-09: suspend a job offering (hidden from search; BC-09 enforces it on JobOfferingSuspended).</summary>
public sealed record SuspendJobOfferingCommand(Guid JobOfferingId, string? Reason) : AdminCommand<Guid>;
