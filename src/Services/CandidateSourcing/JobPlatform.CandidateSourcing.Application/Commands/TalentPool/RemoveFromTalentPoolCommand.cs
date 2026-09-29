using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Commands.TalentPool;

/// <summary>US-3.3.3-07: drop a saved candidate.</summary>
public sealed record RemoveFromTalentPoolCommand(Guid Id) : EmployerCommand<Unit>;
