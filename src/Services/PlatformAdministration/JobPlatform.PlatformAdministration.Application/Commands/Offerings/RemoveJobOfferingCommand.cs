namespace JobPlatform.PlatformAdministration.Application.Commands.Offerings;

/// <summary>Proposed (Q-04): remove a job offering. Same guards and event as a suspension, recorded as removed.</summary>
public sealed record RemoveJobOfferingCommand(Guid JobOfferingId, string? Reason) : AdminCommand<Guid>;
